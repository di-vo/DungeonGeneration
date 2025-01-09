using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TreasureScript : MonoBehaviour
{
    void Start()
    {
        float startRotationZ = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0, 0, startRotationZ);
    }

    void Update()
    {
        
    }
}
