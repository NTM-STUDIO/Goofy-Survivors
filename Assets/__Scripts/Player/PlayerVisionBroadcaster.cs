using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerVisionBroadcaster : NetworkBehaviour
{
    private const float syncInterval = 0.2f;
    private const float syncThresholdSqr = 0.15f;
    private static readonly List<PlayerVisionBroadcaster> activeSources = new();

    [Serializable]
    public struct Footprint : INetworkSerializable
    {
        public Vector3 bottomLeft;
        public Vector3 bottomRight;
        public Vector3 topRight;
        public Vector3 topLeft;
        public Vector3 center;
        public float radius;
        public float planeY;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public float fieldOfView;
        public float orthographicSize;
        public float aspectRatio;
        public float nearClip;
        public float farClip;
        public byte flags;

        public bool IsValid => (flags & 0x1) != 0;
        public bool IsOrthographic => (flags & 0x2) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref bottomLeft);
            serializer.SerializeValue(ref bottomRight);
            serializer.SerializeValue(ref topRight);
            serializer.SerializeValue(ref topLeft);
            serializer.SerializeValue(ref center);
            serializer.SerializeValue(ref radius);
            serializer.SerializeValue(ref planeY);
            serializer.SerializeValue(ref cameraPosition);
            serializer.SerializeValue(ref cameraRotation);
            serializer.SerializeValue(ref fieldOfView);
            serializer.SerializeValue(ref orthographicSize);
            serializer.SerializeValue(ref aspectRatio);
            serializer.SerializeValue(ref nearClip);
            serializer.SerializeValue(ref farClip);
            serializer.SerializeValue(ref flags);
        }
    }

    private readonly NetworkVariable<Footprint> networkFootprint = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private Camera cachedCamera;
    private float nextSyncTime;
    private Footprint cachedFootprint;
    private readonly Vector2[] polygon2D = new Vector2[4];
    private readonly Vector3[] polygon3D = new Vector3[4];
    private readonly Plane[] frustumPlanes = new Plane[6];
    private bool frustumValid;
    private float lastServerUpdateTime;

    public static System.Collections.Generic.IReadOnlyList<PlayerVisionBroadcaster> ActiveSources => activeSources;

    public bool HasValidFootprint => cachedFootprint.IsValid;

    public Vector3 PlayerPosition => transform.position;

    public float LastServerUpdateTime => lastServerUpdateTime;

    public Vector3 GroundCenter => cachedFootprint.center;

    public float GroundPlaneY => cachedFootprint.planeY;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        activeSources.Add(this);
        cachedFootprint = networkFootprint.Value;
        UpdateCachedPolygon();
        networkFootprint.OnValueChanged += HandleFootprintChanged;
    }

    public override void OnNetworkDespawn()
    {
        networkFootprint.OnValueChanged -= HandleFootprintChanged;
        activeSources.Remove(this);
        frustumValid = false;
        base.OnNetworkDespawn();
    }

    private void LateUpdate()
    {
        if (!IsOwner || !IsClient || !IsSpawned) return;
        if (Time.time < nextSyncTime) return;
        cachedCamera = cachedCamera != null ? cachedCamera : Camera.main;
        if (cachedCamera == null) return;

        if (!TryBuildFootprint(cachedCamera, out Footprint snapshot)) return;

        if (!AlmostEquals(networkFootprint.Value, snapshot))
        {
            networkFootprint.Value = snapshot;
            cachedFootprint = snapshot;
            UpdateCachedPolygon();
            lastServerUpdateTime = Time.realtimeSinceStartup;
        }

        nextSyncTime = Time.time + syncInterval;
    }

    private static bool AlmostEquals(Footprint a, Footprint b)
    {
        if (a.IsValid != b.IsValid) return false;
        if (!a.IsValid) return true;
    if (a.IsOrthographic != b.IsOrthographic) return false;
        if ((a.bottomLeft - b.bottomLeft).sqrMagnitude > syncThresholdSqr) return false;
        if ((a.bottomRight - b.bottomRight).sqrMagnitude > syncThresholdSqr) return false;
        if ((a.topRight - b.topRight).sqrMagnitude > syncThresholdSqr) return false;
        if ((a.topLeft - b.topLeft).sqrMagnitude > syncThresholdSqr) return false;
        if ((a.center - b.center).sqrMagnitude > syncThresholdSqr) return false;
        if (Mathf.Abs(a.radius - b.radius) > 0.5f) return false;
        if (Mathf.Abs(a.planeY - b.planeY) > 0.1f) return false;
        if ((a.cameraPosition - b.cameraPosition).sqrMagnitude > 0.05f) return false;
        if (Quaternion.Angle(a.cameraRotation, b.cameraRotation) > 1f) return false;
        if (Mathf.Abs(a.fieldOfView - b.fieldOfView) > 0.5f) return false;
        if (Mathf.Abs(a.orthographicSize - b.orthographicSize) > 0.5f) return false;
        if (Mathf.Abs(a.aspectRatio - b.aspectRatio) > 0.05f) return false;
        if (Mathf.Abs(a.nearClip - b.nearClip) > 0.05f) return false;
        if (Mathf.Abs(a.farClip - b.farClip) > 0.5f) return false;
        return true;
    }

    private bool TryBuildFootprint(Camera cam, out Footprint snapshot)
    {
        var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        Vector3 bl = Intersect(cam, plane, 0f, 0f);
        Vector3 br = Intersect(cam, plane, 1f, 0f);
        Vector3 tr = Intersect(cam, plane, 1f, 1f);
        Vector3 tl = Intersect(cam, plane, 0f, 1f);

        snapshot = default;
        if (!IsValidPoint(bl) || !IsValidPoint(br) || !IsValidPoint(tr) || !IsValidPoint(tl))
        {
            return false;
        }

        snapshot.bottomLeft = bl;
        snapshot.bottomRight = br;
        snapshot.topRight = tr;
        snapshot.topLeft = tl;
        snapshot.center = (bl + br + tr + tl) * 0.25f;
        snapshot.radius = Mathf.Sqrt(Mathf.Max(
            (snapshot.center - bl).sqrMagnitude,
            Mathf.Max((snapshot.center - br).sqrMagnitude, Mathf.Max((snapshot.center - tr).sqrMagnitude, (snapshot.center - tl).sqrMagnitude))));
        snapshot.planeY = transform.position.y;
        snapshot.cameraPosition = cam.transform.position;
        snapshot.cameraRotation = cam.transform.rotation;
        snapshot.fieldOfView = cam.fieldOfView;
        snapshot.orthographicSize = cam.orthographicSize;
        snapshot.aspectRatio = cam.aspect;
        snapshot.nearClip = cam.nearClipPlane;
        snapshot.farClip = cam.farClipPlane;
        snapshot.flags = 0x1;
        if (cam.orthographic) snapshot.flags |= 0x2;
        return true;
    }

    private static Vector3 Intersect(Camera cam, Plane plane, float viewportX, float viewportY)
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector3.positiveInfinity;
    }

    private static bool IsValidPoint(Vector3 point)
    {
        return !float.IsNaN(point.x) && !float.IsInfinity(point.x)
            && !float.IsNaN(point.y) && !float.IsInfinity(point.y)
            && !float.IsNaN(point.z) && !float.IsInfinity(point.z);
    }

    private void HandleFootprintChanged(Footprint previous, Footprint next)
    {
        cachedFootprint = next;
        UpdateCachedPolygon();
        lastServerUpdateTime = Time.realtimeSinceStartup;
    }

    private void UpdateCachedPolygon()
    {
        if (!cachedFootprint.IsValid)
        {
            frustumValid = false;
            return;
        }
        polygon2D[0] = new Vector2(cachedFootprint.bottomLeft.x, cachedFootprint.bottomLeft.z);
        polygon2D[1] = new Vector2(cachedFootprint.bottomRight.x, cachedFootprint.bottomRight.z);
        polygon2D[2] = new Vector2(cachedFootprint.topRight.x, cachedFootprint.topRight.z);
        polygon2D[3] = new Vector2(cachedFootprint.topLeft.x, cachedFootprint.topLeft.z);
        polygon3D[0] = cachedFootprint.bottomLeft;
        polygon3D[1] = cachedFootprint.bottomRight;
        polygon3D[2] = cachedFootprint.topRight;
        polygon3D[3] = cachedFootprint.topLeft;
        UpdateFrustumPlanes();
    }

    public bool ContainsGroundPoint(Vector3 worldPos, float tolerance = 0f)
    {
        if (!cachedFootprint.IsValid) return false;
        Vector2 p = new Vector2(worldPos.x, worldPos.z);
        if (PointInTriangle(p, polygon2D[0], polygon2D[1], polygon2D[2], tolerance)) return true;
        if (PointInTriangle(p, polygon2D[0], polygon2D[2], polygon2D[3], tolerance)) return true;
        return false;
    }

    public float GetRecommendedSpawnRadius(float buffer)
    {
        if (!cachedFootprint.IsValid) return buffer;
        return cachedFootprint.radius + buffer;
    }

    public bool HasFreshData(float maxAgeSeconds)
    {
        if (!cachedFootprint.IsValid) return false;
        if (lastServerUpdateTime <= 0f) return false;
        return (Time.realtimeSinceStartup - lastServerUpdateTime) <= maxAgeSeconds;
    }

    public bool IsPointVisible(Vector3 worldPoint, float padding = 0.5f)
    {
        if (!frustumValid) return false;
        float clampedPadding = Mathf.Max(0.05f, padding);
        Bounds bounds = new Bounds(worldPoint, Vector3.one * (clampedPadding * 2f));
        return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
    }

    private void UpdateFrustumPlanes()
    {
        frustumValid = false;
        if (!cachedFootprint.IsValid) return;

        float aspect = Mathf.Max(0.01f, cachedFootprint.aspectRatio);
        float nearClip = Mathf.Max(0.01f, cachedFootprint.nearClip);
        float farClip = Mathf.Max(nearClip + 0.1f, cachedFootprint.farClip);

        Matrix4x4 projection;
        if (cachedFootprint.IsOrthographic)
        {
            float orthoSize = Mathf.Max(0.01f, cachedFootprint.orthographicSize);
            float width = orthoSize * aspect;
            projection = Matrix4x4.Ortho(-width, width, -orthoSize, orthoSize, nearClip, farClip);
        }
        else
        {
            float fov = Mathf.Clamp(cachedFootprint.fieldOfView, 1f, 179f);
            projection = Matrix4x4.Perspective(fov, aspect, nearClip, farClip);
        }

        Matrix4x4 worldToCamera = Matrix4x4.TRS(cachedFootprint.cameraPosition, cachedFootprint.cameraRotation, Vector3.one).inverse;
        Matrix4x4 viewProjection = projection * worldToCamera;
        GeometryUtility.CalculateFrustumPlanes(viewProjection, frustumPlanes);
        frustumValid = true;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c, float tolerance)
    {
        Vector2 v0 = c - a;
        Vector2 v1 = b - a;
        Vector2 v2 = p - a;

        float dot00 = Vector2.Dot(v0, v0);
        float dot01 = Vector2.Dot(v0, v1);
        float dot02 = Vector2.Dot(v0, v2);
        float dot11 = Vector2.Dot(v1, v1);
        float dot12 = Vector2.Dot(v1, v2);

        float denom = dot00 * dot11 - dot01 * dot01;
        if (Mathf.Abs(denom) < 1e-5f) return false;
        float invDenom = 1f / denom;
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;
        return u >= -tolerance && v >= -tolerance && (u + v) <= 1f + tolerance;
    }
}
