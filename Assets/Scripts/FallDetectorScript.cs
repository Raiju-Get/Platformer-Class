using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallDetectorScript : MonoBehaviour
{
    [SerializeField] Transform playerScript;


    private void Update()
    {
        this.transform.position = new Vector2(playerScript.transform.position.x, transform.position.y);
    }
}
