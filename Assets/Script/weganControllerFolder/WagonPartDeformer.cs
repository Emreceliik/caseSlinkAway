using UnityEngine;

public class WagonPartDeformer : MonoBehaviour
{
    [Header("Corner Settings")]
    [SerializeField] private GameObject cornerPiece;
    [SerializeField] private MeshRenderer mainMeshRenderer;

    [Header("Corner Rotations (Inspector'dan ayarlanabilir)")]
    public float rightToBack = 180f;
    public float backToLeft = 180f;
    public float leftToForward = 180f;
    public float forwardToRight = 180f;
    public float backToRight = 90f;
    public float leftToBack = 90f;
    public float forwardToLeft = 90f;
    public float rightToForward = 90f;

    public void SetCornerState(bool isCorner, Vector3Int from, Vector3Int to)
    {
        if (cornerPiece != null && mainMeshRenderer != null)
        {
            if (isCorner)
            {
                cornerPiece.SetActive(true);
                mainMeshRenderer.enabled = false;

                float rotY = 0f;
                if (from == Vector3Int.right && to == Vector3Int.back) rotY = rightToBack;
                else if (from == Vector3Int.back && to == Vector3Int.left) rotY = backToLeft;
                else if (from == Vector3Int.left && to == Vector3Int.forward) rotY = leftToForward;
                else if (from == Vector3Int.forward && to == Vector3Int.right) rotY = forwardToRight;
                else if (from == Vector3Int.back && to == Vector3Int.right) rotY = backToRight;
                else if (from == Vector3Int.left && to == Vector3Int.back) rotY = leftToBack;
                else if (from == Vector3Int.forward && to == Vector3Int.left) rotY = forwardToLeft;
                else if (from == Vector3Int.right && to == Vector3Int.forward) rotY = rightToForward;

                cornerPiece.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            }
            else
            {
                cornerPiece.SetActive(false);
                mainMeshRenderer.enabled = true;
            }
        }
    }
}