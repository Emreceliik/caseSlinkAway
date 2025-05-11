using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class weganController : MonoBehaviour
{
    #region Inspector Variables
    [Header("Wagon Parts")]
    [SerializeField] private GameObject wagonHead;
    [SerializeField] private List<GameObject> wagonBodyParts = new List<GameObject>();

    [Header("Movement Settings")]
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float moveDuration = 0.1f;
    [SerializeField] private float cornerRotationAngle = 30f;
    [SerializeField] private float mouseFollowThreshold = 0.1f;
    [SerializeField] private float moveInterval = 0.08f;

    [Header("Sink Animation Settings")]
    [SerializeField] private float headSinkDuration = 0.3f;
    [SerializeField] private float headDisappearDuration = 0.2f;
    [SerializeField] private float bodyMoveDuration = 0.4f;
    [SerializeField] private float bodyScaleDuration = 0.2f;
    [SerializeField] private float bodyWaitDuration = 0.1f;
    [SerializeField] private float bodySinkDuration = 0.3f;
    [SerializeField] private float bodyDisappearDuration = 0.2f;
    [SerializeField] private float sinkDistance = 0.5f;
    [SerializeField] private float disappearDistance = 2f;
    [SerializeField] private float scaleUpAmount = 1.1f;
    [SerializeField] private float scaleDownAmount = 0.8f;
    [SerializeField] private float sinkTriggerDelay = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    [SerializeField] private Transform headPoint;
    [SerializeField] private Transform bodyPoint;
    [SerializeField] private Transform legsPoint;
    [SerializeField] private float sinkDuration = 0.5f;
    [SerializeField] private float scaleDuration = 0.3f;

    [SerializeField] private float bendColliderThreshold = 0.2f;
    [SerializeField] private float colliderShrinkFactor = 0.7f;
    #endregion

    #region Public Properties
    public static bool isMoving = false;
    #endregion

    #region Private Variables
    private List<Vector3> positionHistory = new List<Vector3>();
    private List<CornerInfo> cornerInfos = new List<CornerInfo>();
    private Vector3 lastMoveDirection;
    private Vector3 currentMoveDirection;
    private bool isDragging = false;
    private Vector3 mouseWorldPosition;
    private Vector3 gridAlignedHeadPosition;
    private Vector3 targetGridPosition;
    private float nextMoveTime = 0f;
    private LayerMask groundLayerMask;
    private float bodyRotateDuration = 0.12f;
    private List<Vector3> lastBodyTargetPositions = new List<Vector3>();
    private List<string> ignoreTags = new List<string>();
    private Quaternion lastPartInitialRotation;
    private float currentStayTime = 0f;
    private bool isOnSinkTrigger = false;
    private Collider currentSinkTrigger = null;
    private bool isReadyToSink = false;
    private bool isSinking = false;
    private bool isExitingLine = false;
    private Vector3 exitTargetOffset = new Vector3(0, -2f, 0);
    private BoxCollider boxCollider;
    private Vector3 originalColliderSize;
    private Vector3 originalColliderCenter;
    #endregion

    #region Events
    public delegate void WagonSinkCompleteHandler();
    public event WagonSinkCompleteHandler OnWagonSinkComplete;
    #endregion

    #region Unity Methods
    private void Start()
    {
        InitializeWagon();
        groundLayerMask = LayerMask.GetMask("Default");
        lastBodyTargetPositions = new List<Vector3>(new Vector3[wagonBodyParts.Count]);

        if (wagonBodyParts.Count > 0)
        {
            lastPartInitialRotation = wagonBodyParts[wagonBodyParts.Count - 1].transform.rotation;
        }

        FixInitialRotations();
        InitializePartDeformers();
    }

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            originalColliderSize = boxCollider.size;
            originalColliderCenter = boxCollider.center;
        }
    }

    private void Update()
    {
        if (isSinking)
            return;

        if (isOnSinkTrigger && currentSinkTrigger != null)
        {
            currentStayTime += Time.deltaTime;
            if (currentStayTime >= sinkTriggerDelay)
            {
                isReadyToSink = true;
                SinkWagenIntoGround(transform);
                isOnSinkTrigger = false;
                currentSinkTrigger = null;
                currentStayTime = 0f;
            }
        }

        if (isReadyToSink)
            return;

        HandleMouseInput();

        if (isDragging)
        {
            DetermineNextGridPosition();
            MoveHeadToTarget();
        }

        UpdateBodyPartPositions();

        if (Time.frameCount % 60 == 0)
        {
            CleanupOldCorners();
        }

        if (showDebugInfo)
        {
            VisualizeDebugInfo();
        }

        if (isExitingLine)
        {
            Vector3 headTarget = wagonHead.transform.position + exitTargetOffset;
            wagonHead.transform.position = Vector3.Lerp(wagonHead.transform.position, headTarget, Time.deltaTime * 2f);

            for (int i = 0; i < wagonBodyParts.Count; i++)
            {
                GameObject bodyPart = wagonBodyParts[i];
                Vector3 targetPos = (i == 0) ? wagonHead.transform.position : wagonBodyParts[i - 1].transform.position;
                bodyPart.transform.position = Vector3.Lerp(bodyPart.transform.position, targetPos, Time.deltaTime * 8f);

                Vector3 directionForward = (targetPos - bodyPart.transform.position).normalized;
                if (directionForward != Vector3.zero)
                {
                    float angle = Mathf.Atan2(directionForward.x, directionForward.z) * Mathf.Rad2Deg;
                    Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);
                    bodyPart.transform.rotation = targetRotation;
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("SinkTrigger"))
        {
            isOnSinkTrigger = true;
            currentSinkTrigger = other;
            currentStayTime = 0f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("SinkTrigger"))
        {
            isOnSinkTrigger = false;
            currentSinkTrigger = null;
            currentStayTime = 0f;
            isReadyToSink = false;
        }
    }
    #endregion

    #region Private Methods
    private void InitializeWagon()
    {
        if (wagonHead == null)
        {
            Debug.LogError("Wagon head reference is missing!");
            return;
        }

        gridAlignedHeadPosition = wagonHead.transform.position;
        targetGridPosition = gridAlignedHeadPosition;

        positionHistory.Clear();
        positionHistory.Add(gridAlignedHeadPosition);

        Vector3 pathDirection = Vector3.right;

        if (wagonBodyParts.Count > 0)
        {
            Vector3 directionToFirstPart = (wagonBodyParts[0].transform.position - wagonHead.transform.position).normalized;

            if (Mathf.Abs(directionToFirstPart.x) > Mathf.Abs(directionToFirstPart.z))
            {
                pathDirection = new Vector3(Mathf.Sign(directionToFirstPart.x), 0, 0);
            }
            else
            {
                pathDirection = new Vector3(0, 0, Mathf.Sign(directionToFirstPart.z));
            }
        }

        for (int i = 0; i < wagonBodyParts.Count; i++)
        {
            positionHistory.Add(wagonBodyParts[i].transform.position);
        }

        lastMoveDirection = -pathDirection;
        currentMoveDirection = -pathDirection;

        FixInitialRotations();
    }

    private void FixInitialRotations()
    {
        Vector3 headRotation = wagonHead.transform.rotation.eulerAngles;
        float roundedY = Mathf.Round(headRotation.y / 90f) * 90f;
        wagonHead.transform.rotation = Quaternion.Euler(0, roundedY, 0);

        for (int i = 0; i < wagonBodyParts.Count; i++)
        {
            GameObject bodyPart = wagonBodyParts[i];
            Vector3 bodyRotation = bodyPart.transform.rotation.eulerAngles;

            if (i == wagonBodyParts.Count - 1)
            {
                lastPartInitialRotation = bodyPart.transform.rotation;
            }
            else
            {
                roundedY = Mathf.Round(bodyRotation.y / 90f) * 90f;
                bodyPart.transform.rotation = Quaternion.Euler(0, roundedY, 0);
            }
        }
    }

    private void HandleMouseInput()
    {
        if (isReadyToSink)
            return;

        Vector3 mousePos = Input.mousePosition;
        if (mousePos.x < 0 || mousePos.x > Screen.width || mousePos.y < 0 || mousePos.y > Screen.height)
        {
            isDragging = false;
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayerMask))
        {
            mouseWorldPosition = new Vector3(hit.point.x, wagonHead.transform.position.y, hit.point.z);
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray headRay = Camera.main.ScreenPointToRay(mousePos);
            RaycastHit headHit;
            if (Physics.Raycast(headRay, out headHit, Mathf.Infinity, groundLayerMask))
            {
                if (headHit.collider.gameObject == wagonHead)
                {
                    isDragging = true;
                }
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }

    private void DetermineNextGridPosition()
    {
        if (Time.time < nextMoveTime)
            return;

        Vector3 headPos = wagonHead.transform.position;
        gridAlignedHeadPosition = new Vector3(
            Mathf.Round(headPos.x / gridSize) * gridSize,
            headPos.y,
            Mathf.Round(headPos.z / gridSize) * gridSize
        );

        float distanceToMouse = Vector3.Distance(
            new Vector3(gridAlignedHeadPosition.x, 0, gridAlignedHeadPosition.z),
            new Vector3(mouseWorldPosition.x, 0, mouseWorldPosition.z)
        );

        if (distanceToMouse > mouseFollowThreshold)
        {
            Vector3 directionToMouse = (mouseWorldPosition - gridAlignedHeadPosition).normalized;
            directionToMouse.y = 0;

            Vector3 moveDirection;

            if (Mathf.Abs(directionToMouse.x) >= Mathf.Abs(directionToMouse.z))
            {
                moveDirection = new Vector3(Mathf.Sign(directionToMouse.x), 0, 0);
            }
            else
            {
                moveDirection = new Vector3(0, 0, Mathf.Sign(directionToMouse.z));
            }

            Vector3 nextPosition = gridAlignedHeadPosition + moveDirection * gridSize;

            if (Vector3.Distance(gridAlignedHeadPosition, nextPosition) >= gridSize * 0.9f && IsMoveSafe(nextPosition))
            {
                if (moveDirection != currentMoveDirection)
                {
                    cornerInfos.Add(new CornerInfo(gridAlignedHeadPosition, currentMoveDirection, moveDirection));
                    lastMoveDirection = currentMoveDirection;
                    currentMoveDirection = moveDirection;
                }

                targetGridPosition = nextPosition;
                nextMoveTime = Time.time + moveInterval;
                isMoving = true;
            }
        }
    }

    private void MoveHeadToTarget()
    {
        if (!isMoving) return;

        wagonHead.transform.DOKill();
        wagonHead.transform.DOMove(targetGridPosition, moveDuration).SetEase(Ease.OutQuad);
        gridAlignedHeadPosition = targetGridPosition;

        StorePositionHistory();

        if (currentMoveDirection != Vector3.zero)
        {
            float angle = Mathf.Atan2(currentMoveDirection.x, currentMoveDirection.z) * Mathf.Rad2Deg;
            wagonHead.transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        isMoving = false;
    }

    private void StorePositionHistory()
    {
        if (positionHistory.Count == 0 || positionHistory[0] != gridAlignedHeadPosition)
        {
            positionHistory.Insert(0, gridAlignedHeadPosition);
        }

        while (positionHistory.Count > wagonBodyParts.Count + 1)
        {
            positionHistory.RemoveAt(positionHistory.Count - 1);
        }

        for (int i = 0; i < positionHistory.Count; i++)
        {
            Vector3 pos = positionHistory[i];
            pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
            pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
            positionHistory[i] = pos;
        }
    }

    private void UpdateBodyPartPositions()
    {
        if (wagonBodyParts.Count == 0) return;

        // 1. Pozisyon zinciri: Her vagon bir öncekinin pozisyonunu takip eder (grid mantığı)
        for (int i = 0; i < wagonBodyParts.Count; i++)
        {
            if (i + 1 < positionHistory.Count)
            {
                GameObject bodyPart = wagonBodyParts[i];
                Vector3 targetPos = positionHistory[i + 1];
                bodyPart.transform.position = targetPos;

                // Rotasyon (yön) ayarı
                Vector3 directionForward = Vector3.zero;
                if (i + 2 < positionHistory.Count)
                {
                    directionForward = (positionHistory[i + 1] - positionHistory[i + 2]).normalized;
                }
                else if (i + 1 < positionHistory.Count)
                {
                    directionForward = (positionHistory[i] - positionHistory[i + 1]).normalized;
                }

                if (directionForward != Vector3.zero)
                {
                    float angle = Mathf.Atan2(directionForward.x, directionForward.z) * Mathf.Rad2Deg;
                    Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);
                    bodyPart.transform.rotation = targetRotation;
                }
            }
        }

        // 2. Köşe kontrolü: Sadece gerçek köşe noktasında köşe mesh aktif
        for (int i = 0; i < wagonBodyParts.Count; i++)
        {
            GameObject bodyPart = wagonBodyParts[i];

            // Önceki vagonun pozisyonu
            Vector3 prevPos = (i == 0) ? wagonHead.transform.position : wagonBodyParts[i - 1].transform.position;
            // Kendi pozisyonu
            Vector3 thisPos = bodyPart.transform.position;
            // Sonraki vagonun pozisyonu (varsa)
            Vector3 nextPos = (i < wagonBodyParts.Count - 1) ? wagonBodyParts[i + 1].transform.position : thisPos;

            // Kendi yönü (önceki vagona göre)
            Vector3 fromDir = (thisPos - prevPos).normalized;
            // Sonraki yön (sonraki vagona göre)
            Vector3 toDir = (nextPos - thisPos).normalized;

            // Grid yönlerine yuvarla
            Vector3Int from = new Vector3Int(Mathf.RoundToInt(fromDir.x), 0, Mathf.RoundToInt(fromDir.z));
            Vector3Int to = new Vector3Int(Mathf.RoundToInt(toDir.x), 0, Mathf.RoundToInt(toDir.z));

            // Sıfır vektör kontrolü
            bool valid = from.sqrMagnitude > 0 && to.sqrMagnitude > 0;

            // Yönler farklıysa köşe
            bool isCorner = valid && from != to;

            var deformer = bodyPart.GetComponent<WagonPartDeformer>();
            if (deformer != null)
                deformer.SetCornerState(isCorner, from, to);
        }
    }
    
    private void CheckCornerRotation(int bodyIndex)
    {
        if (cornerInfos.Count <= 0)
        {
            float currentY = wagonBodyParts[bodyIndex].transform.rotation.eulerAngles.y;
            float roundedY = Mathf.Round(currentY / 90f) * 90f;
            Quaternion targetRotation = Quaternion.Euler(0, roundedY, 0);
            wagonBodyParts[bodyIndex].transform.rotation = Quaternion.Slerp(wagonBodyParts[bodyIndex].transform.rotation, targetRotation, Time.deltaTime * 10f);
            return;
        }

        GameObject bodyPart = wagonBodyParts[bodyIndex];
        Vector3 bodyPos = bodyPart.transform.position;

        for (int j = cornerInfos.Count - 1; j >= 0; j--)
        {
            if (Vector3.Distance(bodyPos, cornerInfos[j].position) < gridSize * 0.5f)
            {
                Vector3 inDir = cornerInfos[j].incomingDirection;
                Vector3 outDir = cornerInfos[j].outgoingDirection;

                float baseAngle = 0;

                if (outDir.x != 0)
                {
                    baseAngle = outDir.x > 0 ? 0 : 180;
                }
                else
                {
                    baseAngle = outDir.z > 0 ? 90 : 270;
                }

                float tiltAngle = 0;

                if (Mathf.Abs(inDir.x) > 0 && Mathf.Abs(outDir.z) > 0)
                {
                    if ((inDir.x > 0 && outDir.z > 0) || (inDir.x < 0 && outDir.z < 0))
                    {
                        tiltAngle = -cornerRotationAngle;
                    }
                    else
                    {
                        tiltAngle = cornerRotationAngle;
                    }
                }
                else if (Mathf.Abs(inDir.z) > 0 && Mathf.Abs(outDir.x) > 0)
                {
                    if ((inDir.z > 0 && outDir.x < 0) || (inDir.z < 0 && outDir.x > 0))
                    {
                        tiltAngle = -cornerRotationAngle;
                    }
                    else
                    {
                        tiltAngle = cornerRotationAngle;
                    }
                }

                Quaternion targetRotation = Quaternion.Euler(0, baseAngle, tiltAngle);

                bodyPart.transform.rotation = Quaternion.Slerp(
                    bodyPart.transform.rotation,
                    targetRotation,
                    Time.deltaTime * 10f
                );

                break;
            }
        }
    }

    private bool IsMoveSafe(Vector3 targetPosition)
    {
        foreach (GameObject bodyPart in wagonBodyParts)
        {
            if (Vector3.Distance(bodyPart.transform.position, targetPosition) < gridSize * 0.5f)
            {
                return false;
            }
        }

        Collider[] hitColliders = Physics.OverlapSphere(targetPosition, gridSize * 0.4f);
        foreach (var collider in hitColliders)
        {
            if (collider.gameObject == wagonHead || wagonBodyParts.Contains(collider.gameObject))
                continue;

            if (collider.CompareTag("Obstacle") || collider.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
                return false;

            if (collider.CompareTag("Purple") || collider.CompareTag("Orange") || collider.CompareTag("Blue") || collider.CompareTag("Red") || collider.CompareTag("Green"))
                return false;
        }

        return true;
    }

    private void CleanupOldCorners()
    {
        List<int> cornersToRemove = new List<int>();

        for (int i = 0; i < cornerInfos.Count; i++)
        {
            bool isTooFar = true;

            if (Vector3.Distance(wagonHead.transform.position, cornerInfos[i].position) < gridSize * 3f)
            {
                isTooFar = false;
            }
            else
            {
                foreach (GameObject bodyPart in wagonBodyParts)
                {
                    if (Vector3.Distance(bodyPart.transform.position, cornerInfos[i].position) < gridSize * 3f)
                    {
                        isTooFar = false;
                        break;
                    }
                }
            }

            if (isTooFar)
            {
                cornersToRemove.Add(i);
            }
        }

        for (int i = cornersToRemove.Count - 1; i >= 0; i--)
        {
            cornerInfos.RemoveAt(cornersToRemove[i]);
        }
    }

    private void VisualizeDebugInfo()
    {
        Debug.DrawLine(
            wagonHead.transform.position,
            gridAlignedHeadPosition,
            Color.yellow
        );

        Debug.DrawLine(wagonHead.transform.position, targetGridPosition, Color.green);

        foreach (var corner in cornerInfos)
        {
            Debug.DrawLine(corner.position, corner.position + Vector3.up * 0.5f, Color.red);
            Debug.DrawRay(corner.position, corner.incomingDirection, Color.blue);
            Debug.DrawRay(corner.position, corner.outgoingDirection, Color.cyan);
        }

        for (int i = 0; i < positionHistory.Count - 1; i++)
        {
            Debug.DrawLine(positionHistory[i], positionHistory[i + 1], Color.magenta);
        }
    }

    private void InitializePartDeformers()
    {
        WagonPartDeformer headDeformer = wagonHead.GetComponent<WagonPartDeformer>();
        if (headDeformer == null)
        {
            headDeformer = wagonHead.AddComponent<WagonPartDeformer>();
        }

        WagonPartDeformer previousDeformer = headDeformer;
        foreach (GameObject bodyPart in wagonBodyParts)
        {
            WagonPartDeformer currentDeformer = bodyPart.GetComponent<WagonPartDeformer>();
            if (currentDeformer == null)
            {
                currentDeformer = bodyPart.AddComponent<WagonPartDeformer>();
            }
            // currentDeformer.SetPreviousPart(previousDeformer);
            previousDeformer = currentDeformer;
        }
    }
    #endregion

    #region Public Methods
    public void AddBodyPart(GameObject newBodyPart)
    {
        if (newBodyPart == null)
        {
            Debug.LogError("Body part reference is missing!");
            return;
        }

        Vector3 referencePosition;
        Quaternion referenceRotation;

        if (wagonBodyParts.Count > 0)
        {
            GameObject lastBodyPart = wagonBodyParts[wagonBodyParts.Count - 1];
            referencePosition = lastBodyPart.transform.position;
            referenceRotation = lastBodyPart.transform.rotation;
        }
        else
        {
            referencePosition = wagonHead.transform.position;
            referenceRotation = wagonHead.transform.rotation;

            Vector3 direction = -currentMoveDirection;
            referencePosition += direction * gridSize;
        }

        newBodyPart.transform.position = referencePosition;
        newBodyPart.transform.rotation = referenceRotation;

        wagonBodyParts.Add(newBodyPart);
        positionHistory.Add(referencePosition);

        WagonPartDeformer newDeformer = newBodyPart.GetComponent<WagonPartDeformer>();
        if (newDeformer == null)
        {
            newDeformer = newBodyPart.AddComponent<WagonPartDeformer>();
        }

        if (wagonBodyParts.Count > 1)
        {
            WagonPartDeformer previousDeformer = wagonBodyParts[wagonBodyParts.Count - 2].GetComponent<WagonPartDeformer>();
            // newDeformer.SetPreviousPart(previousDeformer);
        }
        else
        {
            WagonPartDeformer headDeformer = wagonHead.GetComponent<WagonPartDeformer>();
            // newDeformer.SetPreviousPart(headDeformer);
        }
    }

    public void SinkCharacterIntoWegan(GameObject character)
    {
        StartCoroutine(SinkAnimation(character));
    }

    public void SinkWagenIntoGround(Transform wagen)
    {
        isSinking = true;
        StartCoroutine(SinkWagenAnimation(wagen));
    }

    public void StartExitLineSequence()
    {
        isExitingLine = true;
    }

    
    #endregion

    #region Coroutines
    private IEnumerator SinkAnimation(GameObject character)
    {
        Transform head = character.transform.Find("Head");
        Transform body = character.transform.Find("Body");
        Transform legs = character.transform.Find("Legs");

        if (head != null && body != null && legs != null)
        {
            Sequence headSequence = DOTween.Sequence();
            headSequence.Append(head.DOScale(new Vector3(0.5f, 0.5f, 0.5f), scaleDuration))
                       .Join(head.DOMove(headPoint.position, sinkDuration))
                       .SetEase(Ease.InQuad);

            yield return headSequence.WaitForCompletion();

            Sequence bodySequence = DOTween.Sequence();
            bodySequence.Append(body.DOScale(new Vector3(0.5f, 0.5f, 0.5f), scaleDuration))
                        .Join(body.DOMove(bodyPoint.position, sinkDuration))
                        .SetEase(Ease.InQuad);

            yield return bodySequence.WaitForCompletion();

            Sequence legsSequence = DOTween.Sequence();
            legsSequence.Append(legs.DOScale(new Vector3(0.5f, 0.5f, 0.5f), scaleDuration))
                        .Join(legs.DOMove(legsPoint.position, sinkDuration))
                        .SetEase(Ease.InQuad);

            yield return legsSequence.WaitForCompletion();

            Destroy(character);
        }
        else
        {
            Destroy(character);
        }
    }

    private IEnumerator SinkWagenAnimation(Transform wagen)
    {
        float fallDistance = 3f;
        float fallDuration = 2.0f;
        int segmentCount = wagonBodyParts.Count + 1;
        float segmentDelay = 0.12f;

        List<Vector3> positionQueue = new List<Vector3>();
        Vector3 headStart = wagonHead.transform.position;
        Vector3 headTarget = headStart + Vector3.down * fallDistance;

        for (int i = 0; i < segmentCount * 10; i++)
            positionQueue.Add(headStart);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / fallDuration;
            Vector3 newHeadPos = Vector3.Lerp(headStart, headTarget, t);
            positionQueue.Insert(0, newHeadPos);
            if (positionQueue.Count > segmentCount * 10)
                positionQueue.RemoveAt(positionQueue.Count - 1);

            wagonHead.transform.position = newHeadPos;

            for (int i = 0; i < wagonBodyParts.Count; i++)
            {
                int queueIndex = Mathf.Min((i + 1) * Mathf.RoundToInt(segmentDelay / Time.deltaTime), positionQueue.Count - 1);
                Vector3 targetPos = positionQueue[queueIndex];
                wagonBodyParts[i].transform.position = Vector3.Lerp(wagonBodyParts[i].transform.position, targetPos, Time.deltaTime * 10f);
            }

            yield return null;
        }

        wagonHead.transform.position = headTarget;
        for (int i = 0; i < wagonBodyParts.Count; i++)
        {
            int queueIndex = Mathf.Min((i + 1) * Mathf.RoundToInt(segmentDelay / Time.deltaTime), positionQueue.Count - 1);
            Vector3 targetPos = positionQueue[queueIndex];
            wagonBodyParts[i].transform.position = targetPos;
        }

        Destroy(wagen.gameObject);
    }
    #endregion

    #region Helper Classes
    private class CornerInfo
    {
        public Vector3 position;
        public Vector3 incomingDirection;
        public Vector3 outgoingDirection;

        public CornerInfo(Vector3 pos, Vector3 incoming, Vector3 outgoing)
        {
            position = pos;
            incomingDirection = incoming;
            outgoingDirection = outgoing;
        }
    }
    #endregion
}