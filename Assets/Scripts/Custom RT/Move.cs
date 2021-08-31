using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Move : MonoBehaviour
{
    public float MoveSpeed = 10.0f;

    private Vector3 Direction;
    private float Vertical;
    // Update is called once per frame
    void Update()
    {
        float UP = 0;

        if (Input.GetKey(KeyCode.Space))
        {
            UP = 1.0f;
        }
        else if (Input.GetKey(KeyCode.LeftShift))
        {
            UP = -1.0f;
        }

        Direction = new Vector3(Input.GetAxis("Horizontal"), UP, Input.GetAxis("Vertical")) * MoveSpeed * Time.deltaTime;

        transform.Translate(Direction);
    }
}
