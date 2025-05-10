using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class weganSeatCheck : MonoBehaviour
{
    #region Public Variables
    public List<GameObject> seats = new List<GameObject>();
    #endregion

    #region Private Variables
    private string currentTag = ""; // Mevcut wagen'in tag'i
    #endregion

    #region Unity Methods
    void Start()
    {
        // Başlangıçta wagen'in tag'ini al
        currentTag = transform.parent.tag;
        
        foreach (GameObject seat in seats)
        {
            seat.SetActive(false);
            seat.transform.DOScale(new Vector3(0.3f, 0.3f, 0.3f), 0.5f).SetEase(Ease.OutBack);
        }
    }
    #endregion

    #region Public Methods
    // Tag kontrolü yapan metod
    public bool CanAcceptCharacter(string characterTag)
    {
        // Eğer tag'ler eşleşiyorsa ve boş koltuk varsa true döner
        return characterTag == currentTag && seats.Count > 0;
    }

    public void setActiveAndAnimationScaleSeatandRemoveList()
    {
        seats[0].SetActive(true);
        seats[0].transform.DOScale(new Vector3(1.2f, 1.2f, 1.2f), 0.5f).SetEase(Ease.OutBack);
        seats.RemoveAt(0);
    }
    #endregion
}
