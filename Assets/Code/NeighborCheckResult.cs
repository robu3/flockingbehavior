using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Result of a boid neighbor check.
/// </summary>
public struct NeighborCheckResult
{
    /// <summary>
    /// The separation force (motivation to keeps boids from being on top of one another).
    /// </summary>
    public Vector3 Separation;

    /// <summary>
    /// Flocking force (motivation of boids to flock together).
    /// </summary>
    public Vector3 Flocking;
}
