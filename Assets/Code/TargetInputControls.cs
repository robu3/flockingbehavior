using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetInputControls : MonoBehaviour
{
    public float Speed = 1f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 move = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")) * Speed;
        transform.position += move;
    }
}
