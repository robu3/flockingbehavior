using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlockingBehavior : MonoBehaviour
{
    /// <summary>
    /// The number of boids in the flock.
    /// </summary>
    public int BoidCount;

    /// <summary>
    /// The boid prefab template.
    /// </summary>
    public GameObject BoidPrefab;

    /// <summary>
    /// The boids in the flock.
    /// </summary>
    public List<GameObject> Boids { get; set; }

    /// <summary>
    /// A boid's range of vision.
    /// </summary>
    public float BoidVision = 5f;
    
    /// <summary>
    /// How fast a boid moves.
    /// </summary>
    public float BoidSpeed = 1f;

    /// <summary>
    /// How well a boid steers itself.
    /// </summary>
    public float BoidSteer = 1f;

    /// <summary>
    /// A boid's preferred distance from its brethren.
    /// </summary>
    public float BoidDistance = 1f;

    /// <summary>
    /// Threshold for boid movement; if the motivational force is less than
    /// this number, the boid will ignore it.
    /// </summary>
    public float BoidMoveThreshold = 1f;

    /// <summary>
    /// The size of our subdivided space cells.
    /// </summary>
    public int SubdivisionCellSize = 5;

    /// <summary>
    /// The object the flock is following / attracted to.
    /// </summary>
    public GameObject FollowTarget;

    /// <summary>
    /// A boid's preferred distance from its target.
    /// </summary>
    public float FollowDistance = 1f;

    /// <summary>
    /// The distance from the target at which the boid will begin to stop (slow down).
    /// </summary>
    public float FollowStopDistance = 1f;

    /// <summary>
    /// Motivation to follow the target.
    /// </summary>
    public float MotivationFollow = 1f;

    /// <summary>
    /// Motivation to keep distance between the boid and the target.
    /// </summary>
    public float MotivationFollowSeparate = 1f;

    /// <summary>
    /// Motivation to for boids to keep distance between themselves.
    /// </summary>
    public float MotivationSeparate = 1f;

    /// <summary>
    /// Motivation for boids to flock/congregate together.
    /// </summary>
    public float MotivationFlocking = 1f;

    /// <summary>
    /// Initial boid spawn radius.
    /// </summary>
    public float SpawnRadius = 30f;

    /// <summary>
    /// The "bounds" of the flock. Boids outside this radius will stop moving.
    /// </summary>
    public float FlockRadius = 30f;

    /// <summary>
    /// A rectangular subdivided space of cells containing boids.
    /// This subdivided space is used to optimize the process of boids finding their neighbors in the flock.
    /// </summary>
    List<GameObject>[,] Space { get; set; }

    private Rect _spaceDims;
    private Rect _cellDims;

    // Start is called before the first frame update
    void Start()
    {
        Initialize();
    }

    // Update is called once per frame
    void Update()
    {
        SubdivideSpace();
        Move();
    }

    /// <summary>
    /// Initializes the flock.
    /// </summary>
    private void Initialize()
    {
        this.Boids = new List<GameObject>();

        // spawn boids at a random position in the spawn area
        for (int i = 0; i < this.BoidCount; i++)
        {
            GameObject boid = GameObject.Instantiate(this.BoidPrefab);

            boid.transform.position = new Vector3(Random.value, Random.value) * SpawnRadius;
            boid.transform.parent = this.transform;

            this.Boids.Add(boid);
        }
    }

    /// <summary>
    /// Subdivides the flock space.
    /// </summary>
    private void SubdivideSpace()
    {
        // use bin-lattice spatial subdivision to divide up the flock area
        // find the bounding box
        Vector2 bottomLeft = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 topRight = new Vector2(float.MinValue, float.MinValue);

        foreach (GameObject b in Boids)
        {
            if (b.transform.position.x < bottomLeft.x)
            {
                bottomLeft.x = b.transform.position.x;
            }

            if (b.transform.position.y < bottomLeft.y)
            {
                bottomLeft.y = b.transform.position.y;
            }

            if (b.transform.position.x > topRight.x)
            {
                topRight.x = b.transform.position.x;
            }

            if (b.transform.position.y > topRight.y)
            {
                topRight.y = b.transform.position.y;
            }
        }

        // then subdivide
        Vector2 boxSize = topRight - bottomLeft;
        int cellX = Mathf.RoundToInt(boxSize.x / SubdivisionCellSize) + 1;
        int cellY = Mathf.RoundToInt(boxSize.y / SubdivisionCellSize) + 1;

        Vector2 cellStep = boxSize / SubdivisionCellSize;

        List<GameObject>[,] space = new List<GameObject>[cellX, cellY];

        foreach (GameObject b in Boids)
        {
            // determine containing cell
            // get position relative to bottom left corner
            Vector2 pos = (Vector2)b.transform.position - bottomLeft;
            int x = Mathf.FloorToInt(pos.x / SubdivisionCellSize);
            int y = Mathf.FloorToInt(pos.y / SubdivisionCellSize);

            try
            {
                if (space[x, y] == null)
                {
                    space[x, y] = new List<GameObject>();
                }

                // and add to our subdivided space
                space[x, y].Add(b);
            }
            catch
            {
                Debug.LogWarningFormat("Bad index [{0},{1}]", x, y);
                Debug.Log(boxSize);
                throw;
            }
        }

        this.Space = space;

        // set subdivision dimension for gizmo drawing
        this._spaceDims = new Rect(bottomLeft, boxSize);
    }

    private void Move()
    {
        // for each subdivided space cell
        // have each boid check neighbors in its field of vision
        // and move in the appropriate direction to keep the desired distance
        for (int x = 0; x < Space.GetLength(0); x++)
        {
            for (int z = 0; z < Space.GetLength(1); z++)
            {
                List<GameObject> boids = Space[x, z];

                if (boids != null)
                {
                    foreach (GameObject b in boids)
                    {
                        Rigidbody rb = b.GetComponent<Rigidbody>();

                        if (rb == null)
                        {
                            throw new System.Exception("Boid is missing rigidbody component.");
                        }

                        // fail-safe: stop movement when outside the flock radius
                        if (FlockRadius > 0f && Vector3.Distance(transform.position, b.transform.position) > FlockRadius)
                        {
                            rb.velocity = Vector3.zero;
                            continue;
                        }

                        // calculate and apply our movement forces
                        Vector3 velocity = rb.velocity;
                        Vector3 move = Vector3.zero;

                        // check our neighbors to calculate forces relative to them
                        // we want to be close, but not too close
                        NeighborCheckResult result = CheckNeighbors(b, boids);

                        if (result.Separation.magnitude >= BoidMoveThreshold)
                        {
                            move += result.Separation * MotivationSeparate;
                        }

                        if (result.Flocking.magnitude >= BoidMoveThreshold)
                        {
                            move += result.Flocking * MotivationFlocking;
                        }

                        // calculate forces that draw us to the target
                        Vector3 forceFollow = Follow(b);
                        if (forceFollow.magnitude >= BoidMoveThreshold)
                        {
                            move += forceFollow * MotivationFollow;
                        }

                        // boids still want to maintain some distance from the target
                        Vector3 forceFollowSeparate = FollowSeparate(b);
                        if (forceFollowSeparate.magnitude >= BoidMoveThreshold)
                        {
                            move += forceFollowSeparate * MotivationFollowSeparate;
                        }

                        // apply the combined movement force
                        move = move.normalized * BoidSpeed;

                        Vector3 steer = move - velocity;
                        steer = Limit(steer, BoidSteer);

                        rb.AddForce(steer);

                        // only look towards the follow target
                        b.transform.LookAt(b.transform.position + forceFollow);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Limits a vector's magnitude to the specified maximum.
    /// </summary>
    /// <param name="v"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    private Vector3 Limit(Vector3 v, float max)
    {
        if (v.magnitude > max)
        {
            v = v.normalized * max;
        }

        return v;
    }

    /// <summary>
    /// Maps a value from on range into another.
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <param name="desiredMin"></param>
    /// <param name="desiredMax"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    private float Map(float value, float min, float max, float desiredMin, float desiredMax)
    {
        if (value < min || value > max)
        {
            throw new System.ArgumentException("value is outside of the specified starting range.");
        }

        return (value - min) * (desiredMax - desiredMin) / (max - min) + desiredMin;
    }

    /// <summary>
    /// Checks neighbors and calculates motivational forces.
    /// </summary>
    /// <param name="boid"></param>
    /// <param name="neighbors"></param>
    /// <returns></returns>
    private NeighborCheckResult CheckNeighbors(GameObject boid, List<GameObject> neighbors)
    {
        // you gotta keep 'em separated!
        Vector3 sep = Vector3.zero;
        Vector3 flock = Vector3.zero;

        foreach (GameObject neighbor in neighbors)
        {
            if (boid == neighbor)
            {
                continue;
            }

            // move away from the neighbor if we can see it
            // and it's too close
            Vector3 distance = neighbor.transform.position - boid.transform.position;

            if (distance.magnitude <= BoidVision)
            {
                if (distance.magnitude == 0f)
                {
                    distance = new Vector3(Random.value, Random.value);
                    continue;
                }

                if (distance.magnitude < BoidDistance)
                {
                    // too close, move away
                    sep -= distance.normalized;
                }
                else if (distance.magnitude > BoidDistance)
                {
                    // too far, move closer
                    flock = distance.normalized;
                }
            }
        }

        return new NeighborCheckResult() { Separation = sep, Flocking = flock };
    }

    /// <summary>
    /// Returns the vector directing the boid towards its target.
    /// </summary>
    /// <param name="boid"></param>
    /// <returns></returns>
    private Vector3 Follow(GameObject boid)
    {
        Vector3 move = Vector3.zero;

        if (FollowTarget != null)
        {
            Vector3 distance = FollowTarget.transform.position - boid.transform.position;

            if (distance.magnitude <= BoidVision)
            {
                if (distance.magnitude > FollowDistance)
                {
                    // decrease the magnitude the closer we are to the target
                    float mag;

                    if (distance.magnitude < FollowStopDistance)
                    {
                        mag = Map(distance.magnitude, 0, FollowStopDistance, 0, BoidSpeed);
                    }
                    else
                    {
                        mag = distance.magnitude;
                    }

                    move = distance.normalized * mag;
                }
            }
        }

        return move;
    }

    /// <summary>
    /// Returns the vector keeping the boid at the correct distance from the target.
    /// </summary>
    /// <param name="boid"></param>
    /// <returns></returns>
    private Vector3 FollowSeparate(GameObject boid)
    {
        Vector3 move = Vector3.zero;

        if (FollowTarget != null)
        {
            Vector3 distance = FollowTarget.transform.position - boid.transform.position;

            if (distance.magnitude <= BoidVision)
            {
                if (distance.magnitude < FollowDistance)
                {
                    // we're too close to the target, so move away from it
                    // if on top of the target, back away in a random direction
                    if (distance.magnitude == 0f)
                    {
                        distance = new Vector3(Random.value, Random.value);
                    }
                    else
                    {
                        distance = -distance;
                    }

                    move = distance;
                }
            }
        }

        return move.normalized;
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, SpawnRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, FlockRadius);
    }

    public void OnDrawGizmosSelected()
    {
        // draw subdivided space
        Gizmos.color = Color.magenta;

        // outer box
        int cellX = Mathf.RoundToInt(_spaceDims.size.x / SubdivisionCellSize) + 1;
        int cellY = Mathf.RoundToInt(_spaceDims.size.y / SubdivisionCellSize) + 1;
        Rect outer = new Rect(_spaceDims.min, new Vector2(cellX * SubdivisionCellSize, cellY * SubdivisionCellSize));
        Gizmos.DrawWireCube(outer.center, outer.size);


        // columns
        for (int x = 1; x <= cellX; x++)
        {
            float posX = outer.min.x - (SubdivisionCellSize * .5f) + (x * SubdivisionCellSize);
            Gizmos.DrawWireCube(new Vector3(posX, outer.center.y), new Vector3(SubdivisionCellSize, outer.size.y));
        }

        // rows
        for (int y = 1; y <= cellY; y++)
        {
            float posY = outer.min.y - (SubdivisionCellSize * .5f) + (y * SubdivisionCellSize);
            Gizmos.DrawWireCube(new Vector3(outer.center.x, posY), new Vector3(outer.size.x, SubdivisionCellSize));
        }
    }
}
