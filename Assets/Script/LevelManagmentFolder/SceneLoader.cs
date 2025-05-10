using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneLoader : MonoBehaviour
{
  [SerializeField] private string sceneToLoad;

  public void LoadScene()
  {
    // Load the scene with the name stored in sceneToLoad
    UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
  }
}
