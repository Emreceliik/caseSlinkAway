using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System;

public class exitLine : MonoBehaviour
{
    #region Inspector Variables
    [SerializeField] private string weganTag = "Wegan";
    [SerializeField] private float checkInterval = 0.1f;
    [SerializeField] private float cooldownDuration = 0.1f;
    [SerializeField] private float checkRadius = 0.5f;
    #endregion

    #region Public Properties
    public bool IsWeganFull { get; private set; }
    #endregion

    #region Private Variables
    private bool isOnCooldown = false;
    private float lastCheckTime = 0f;
    private float lastPickupTime = 0f;
    private weganController currentWagon;
    #endregion

    #region Unity Methods
    void Update()
    {
        if (isOnCooldown && Time.time - lastPickupTime >= cooldownDuration)
        {
            isOnCooldown = false;
        }

        if (!IsWeganFull && !isOnCooldown && Time.time - lastCheckTime >= checkInterval)
        {
            checkWeganIsFull();
            lastCheckTime = Time.time;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + new Vector3(0, 0.5f, 0), checkRadius);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Wagon"))
        {
            currentWagon = other.GetComponent<weganController>();
            if (currentWagon != null)
            {
                currentWagon.OnWagonSinkComplete += OnWagonSinkComplete;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Wagon"))
        {
            if (currentWagon != null)
            {
                currentWagon.OnWagonSinkComplete -= OnWagonSinkComplete;
                currentWagon = null;
            }
        }
    }

    private void OnDestroy()
    {
        if (currentWagon != null)
        {
            currentWagon.OnWagonSinkComplete -= OnWagonSinkComplete;
        }
    }
    #endregion

    #region Private Methods
    void checkWeganIsFull()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position + new Vector3(0, 0.5f, 0), checkRadius, LayerMask.GetMask("Default"));
        
        foreach (var hitCollider in hitColliders)
        {
            GameObject hitObject = hitCollider.gameObject;

            if (hitObject.CompareTag(weganTag))
            {
                Transform parent = hitObject.transform.parent;
                if (parent != null)
                {
                    weganSeatCheck seatCheck = parent.GetComponentInChildren<weganSeatCheck>();
                    weganController wagenController = parent.GetComponentInChildren<weganController>();
                    
                    if (seatCheck != null && seatCheck.seats.Count == 0)
                    {
                        if (wagenController != null)
                        {
                            wagenController.SinkWagenIntoGround(parent);
                            isOnCooldown = true;
                            lastPickupTime = Time.time;
                            wagenController.OnWagonSinkComplete += OnWagonSinkComplete;
                        }
                        else
                        {
                            moveWeganDownAndDestroy(parent);
                        }
                        break;
                    }
                }
            }
        }
    }

    void moveWeganDownAndDestroy(Transform wegan)
    {
        wegan.DOMoveY(-10, 1f).SetEase(Ease.InOutSine).OnComplete(() => {
            IsWeganFull = true;
        });
        Destroy(wegan.gameObject, 1f);
    }

    private void OnWagonSinkComplete()
    {
        IsWeganFull = true;
    }
    #endregion
}
