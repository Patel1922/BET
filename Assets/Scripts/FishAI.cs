using UnityEngine;

public class FishAI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public float swimSpeed = 2f;
    public float swimDistance = 3f;

    private Vector3 startingPosition;
    private int direction = 1;
    void Start()
    {
        startingPosition = transform.position;
        
    }

    // Update is called once per frame
    void Update()
    {
        // Moving the fish horizontally
        transform.position += Vector3.right * direction * swimSpeed * Time.deltaTime;

        // Change direction when fish reaches its swimming limit
        if(transform.position.x >= startingPosition.x + swimDistance)
        {
            direction = -1; // change direction by U turn
        }
        else if (transform.position.x <= startingPosition.x - swimDistance)
        {
            direction = 1 ; // go same direction
        } 
    }
}
