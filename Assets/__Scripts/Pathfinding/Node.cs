using UnityEngine;

public class Node
{
    // Node properties
    public bool walkable;     // Can this node be walked on?
    public Vector3 worldPosition; // Position in the world
    public int gridX, gridZ;  // Position in the grid
    
    // A* pathfinding properties
    public int gCost;         // Cost from start node
    public int hCost;         // Estimated cost to end node
    public Node parent;       // Parent node for path reconstruction

    // Constructor
    public Node(bool _walkable, Vector3 _worldPos, int _gridX, int _gridZ)
    {
        walkable = _walkable;
        worldPosition = _worldPos;
        gridX = _gridX;
        gridZ = _gridZ;
    }

    // Total cost for A* (f = g + h)
    public int fCost
    {
        get { return gCost + hCost; }
    }
}
