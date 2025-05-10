using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class passagesCheck : MonoBehaviour
{
    #region Inspector Variables
    [SerializeField] private List<GameObject> characters = new List<GameObject>();
    [SerializeField] private float moveDuration = 0.3f; 
    [SerializeField] private float queueUpdateDuration = 0.05f; 
    [SerializeField] private float raycastDistance = 0.6f;
    [SerializeField] private float checkInterval = 0.1f;
    [SerializeField] private float cooldownDuration = 0.1f; 

    [Header("Materials")]
    [SerializeField] private Material purpleMaterial;
    [SerializeField] private Material orangeMaterial;
    [SerializeField] private Material blueMaterial;
    [SerializeField] private Material redMaterial;
    [SerializeField] private Material greenMaterial;
    #endregion

    #region Private Variables
    private GameObject wegan;
    private bool isMoving = false;
    private bool isOnCooldown = false;
    private float lastPickupTime = 0f;
    private List<Vector3> originalPositions = new List<Vector3>();
    private List<Quaternion> originalRotations = new List<Quaternion>();
    private float lastCheckTime = 0f;
    private string currentTargetTag = ""; 
    private Material currentMaterial;
    #endregion

    #region Unity Methods
    void Start()
    {
        UpdateMaterialBasedOnFirstCharacter();
    }

    void Update()
    {
        if (isOnCooldown && Time.time - lastPickupTime >= cooldownDuration)
        {
            isOnCooldown = false;
        }

        if (!isMoving && !isOnCooldown && Time.time - lastCheckTime >= checkInterval)
        {
            checkRayWithPassages();
            lastCheckTime = Time.time;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + new Vector3(0, 0.5f, 0), transform.forward * raycastDistance);
    }
    #endregion

    #region Private Methods
    private void UpdateMaterialBasedOnFirstCharacter()
    {
        if (characters.Count > 0)
        {
            string tag = characters[0].tag;
            Material newMaterial = GetMaterialForTag(tag);
            
            if (newMaterial != null)
            {
                Renderer renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = newMaterial;
                    currentMaterial = newMaterial;
                }
            }
        }
    }

    private Material GetMaterialForTag(string tag)
    {
        switch (tag.ToLower())
        {
            case "purple":
                return purpleMaterial;
            case "orange":
                return orangeMaterial;
            case "blue":
                return blueMaterial;
            case "red":
                return redMaterial;
            case "green":
                return greenMaterial;
            default:
                return null;
        }
    }

    void checkRayWithPassages()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + new Vector3(0, 0.5f, 0), transform.forward, out hit, raycastDistance, LayerMask.GetMask("Default")))
        {
            wegan = hit.collider.gameObject;
            
            if (currentTargetTag != hit.collider.gameObject.tag)
            {
                currentTargetTag = hit.collider.gameObject.tag;
            }

            Transform parent = wegan.transform.parent;
            if (parent != null)
            {
                weganSeatCheck seatCheck = parent.GetComponentInChildren<weganSeatCheck>();
                
                if (seatCheck != null && seatCheck.seats.Count == 0)
                {
                    return;
                }
            }

            if (characters.Count > 0 && 
                characters[0].CompareTag(currentTargetTag) && 
                hit.collider.gameObject.CompareTag(currentTargetTag))
            {
                SaveOriginalTransforms();
                moveCharacterToWegan(characters[0]);
                isOnCooldown = true;
                lastPickupTime = Time.time;
            }
        }
    }

    void SaveOriginalTransforms()
    {
        originalPositions.Clear();
        originalRotations.Clear();
        foreach (GameObject character in characters)
        {
            originalPositions.Add(character.transform.position);
            originalRotations.Add(character.transform.rotation);
        }
    }

    void moveCharacterToWegan(GameObject character)
    {
        isMoving = true;
        int removedIndex = characters.IndexOf(character);
        
        character.transform.DOJump(wegan.transform.position, 1f, 1, moveDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => {
                Transform parent = wegan.transform.parent;
                if (parent != null)
                {
                    weganSeatCheck seatCheck = parent.GetComponentInChildren<weganSeatCheck>();
                    if (seatCheck != null)
                    {
                        seatCheck.setActiveAndAnimationScaleSeatandRemoveList();
                    }

                    weganController wagenController = parent.GetComponent<weganController>();
                    if (wagenController != null)
                    {
                        wagenController.SinkCharacterIntoWegan(character);
                    }
                    else
                    {
                        characters.Remove(character);
                        Destroy(character);
                    }
                }
                else
                {
                    characters.Remove(character);
                    Destroy(character);
                }

                StartCoroutine(MoveCharactersOneByOne(removedIndex));
                UpdateMaterialBasedOnFirstCharacter();
            });
    }
    #endregion

    #region Coroutines
    IEnumerator MoveCharactersOneByOne(int removedIndex)
    {
        for (int i = removedIndex; i < characters.Count; i++)
        {
            GameObject currentChar = characters[i];
            Vector3 targetPos = originalPositions[i];
            Quaternion targetRot = originalRotations[i];

            yield return currentChar.transform.DOMove(targetPos, queueUpdateDuration)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();
            
            currentChar.transform.rotation = targetRot;
        }

        isMoving = false;
    }
    #endregion
}

