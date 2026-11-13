using UnityEngine;

public class FishAI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public float swimSpeed = 2f;
    public float swimDistance = 3f;

    private Vector3 startingPosition;
    private int direction = 1;

    //bait detection
    public Transform bait;
    public float detectionRange = 7f;
    void Start()
    {
        startingPosition = transform.position;
        
    }

    // Update is called once per frame
    void Update()
    {
        //check for nearby bait 
    if(bait != null && Vector2.Distance(transform.position, bait.position)<= detectionRange)
    {
        //fish approaches the bait
        transform.position = Vector3.MoveTowards ( transform.position, bait.position, swimSpeed * Time.deltaTime);
    }
    else
    {
        // no bait in range, keep swimming
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
}
