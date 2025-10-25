using UnityEngine;

public class Grid : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Width of the grid in world units")]
    public int gridWorldSizeX = 50;
    [Tooltip("Height of the grid in world units")]
    public int gridWorldSizeZ = 50;
    [Tooltip("Size of each node in the grid")]
    public float nodeRadius = 0.5f;
    
    [Header("Collision Settings")]
    [Tooltip("Layer mask for detecting unwalkable areas")]
    public LayerMask unwalkableMask;

    // Grid properties
    private Node[,] grid;
    private float nodeDiameter;
    private int gridSizeX, gridSizeZ;

    private void Awake()
    {
        // Calculate node size and grid dimensions
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSizeX / nodeDiameter);
        gridSizeZ = Mathf.RoundToInt(gridWorldSizeZ / nodeDiameter);
        
        // Create the grid when the game starts
        CreateGrid();
    }

    void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeZ];
        // Calculate bottom left corner of the grid
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSizeX/2 - Vector3.forward * gridWorldSizeZ/2;

        // Create all nodes
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) 
                                                   + Vector3.forward * (z * nodeDiameter + nodeRadius);
                
                // Check if position is walkable using a sphere cast
                bool walkable = !Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask);
                
                // Create new node
                grid[x, z] = new Node(walkable, worldPoint, x, z);
            }
        }
    }

    // Get node from world position
    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        // Calculate percentage position in grid
        float percentX = (worldPosition.x + gridWorldSizeX/2) / gridWorldSizeX;
        float percentZ = (worldPosition.z + gridWorldSizeZ/2) / gridWorldSizeZ;
        
        // Clamp values
        percentX = Mathf.Clamp01(percentX);
        percentZ = Mathf.Clamp01(percentZ);

        // Convert to grid coordinates
        int x = Mathf.RoundToInt((gridSizeX-1) * percentX);
        int z = Mathf.RoundToInt((gridSizeZ-1) * percentZ);
        
        return grid[x,z];
    }

    // Get neighboring nodes
    public System.Collections.Generic.List<Node> GetNeighbors(Node node)
    {
        var neighbors = new System.Collections.Generic.List<Node>();

        // Check all 8 surrounding nodes
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;

                int checkX = node.gridX + x;
                int checkZ = node.gridZ + z;

                // Check if node is within grid bounds
                if (checkX >= 0 && checkX < gridSizeX && checkZ >= 0 && checkZ < gridSizeZ)
                {
                    neighbors.Add(grid[checkX, checkZ]);
                }
            }
        }

        return neighbors;
    }

    // Get grid size for pathfinding calculations
    public int MaxSize
    {
        get { return gridSizeX * gridSizeZ; }
    }
}
