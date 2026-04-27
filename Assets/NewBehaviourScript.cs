using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log(0);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log(1);
    }
}
