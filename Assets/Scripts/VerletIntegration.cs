using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class VerletIntegration : MonoBehaviour
{
    [Serializable]
    public class Point 
    {
        public Vector3 pos;
        public Vector3 oldPos;
        public Renderer visual;
        public Color color = Color.white;
        public bool pinned;
        public Vector3 Velocity => pos - oldPos;

        public Point()
        {
            
        }
        
        public void Pin()
        {
            this.pinned = true;
            oldPos = pos;
        }
    }

    public class Stick
    {
        public Point p0,p1;
        public float length;
        public LineRenderer visual;
    }

    public List<Point> points = new List<Point>();
    public List<Stick> sticks = new List<Stick>();
    public Bounds bounds = new Bounds(new Vector3(0,0,0), new Vector3(5,5,5));
    public GameObject pointPrefab;
    public LineRenderer stickPrefab;
    public float bounce = .9f;
    public Vector3 gravity = new Vector3(0, -0.5f, 0);
    public Vector3 wind = new Vector3(.25f, 0, 0);
    public float WindNoiseFrequency = 1;
    public Vector2 WindowNoiseRange = new Vector2(.75f, 1);
    [FormerlySerializedAs("WindNoiseStrength")] public float NoiseStrength = 1;
    public float friction = 0.999f;
    public int NumIterations = 3;

    public Vector3 ClothPosition = new Vector3(0,4,0);
    public float ClothWidth = 2.5f;
    public float ClothHeight = 2.5f;
    public int PointsPerUnit = 4;

    [Header("Flag Parameters")] 
    public float FlagPoleHeight = 4;

    public float FlagWidth = 3;
    public float FlagAspectRatio = 10 / 19f;
    public float FlagHeight => FlagWidth * FlagAspectRatio;

    [Header("Pinned Grid")] 
    public float PinnedGridWidth = 4;
    public float PinnedGridHeight = 4;
    public int PinnedGridPointsPerPin = 4;
    public Vector2 VelocityRangeInput = new Vector2(0,.2f);
    public Vector2 VelocityRangeOutput = new Vector2(0,1.5f);
    public float ColorChangeSpeed = 1;
    
    public float strengthOfPush = .01f;
    public float radiusOfPush = 1;
    
    public GameObject sparkPrefab;
    public float SpawnRatePerSecond = 2;

    private Camera mainCamera;
    private 
    void Start()
    {
        InitPoints();
        mainCamera = FindObjectOfType<Camera>();
    }
    
    void Update()
    {
        if(Application.isMobilePlatform)
            HandleMobileInput();
        else
            HandleDesktopInput();
        
        
        UpdateRenderPos();
    }

    private void HandleMobileInput() 
    {
        foreach (Touch touch in Input.touches)
        {
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
            {
                ProcessTouchPosition(new Vector3(touch.position.x, touch.position.y, 0));
            }
        }
    }

    private void ProcessTouchPosition(Vector3 pos) 
    {
        Ray ray = mainCamera.ScreenPointToRay(pos);

        var clothPlane = new Plane(new Vector3(0, 0, 1), ClothPosition);
        if (clothPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            foreach (Point p in points)
            {
                // Assume this is on the XY plane at Z=0
                Vector3 delta = p.pos - hitPoint;
                delta.z = 0;
                float sqrMag = delta.sqrMagnitude;
                if (sqrMag < radiusOfPush * radiusOfPush)
                {
                    float strengthFalloff = (radiusOfPush - Mathf.Sqrt(sqrMag)) / radiusOfPush;
                    p.oldPos -= ray.direction * (strengthFalloff * strengthOfPush);
                }
            }

            float spawnChance = SpawnRatePerSecond * Time.deltaTime;
            if(Random.value < spawnChance)
            {
                Instantiate(sparkPrefab, hitPoint, Quaternion.identity, transform);
            }
        }
    }

    private void HandleDesktopInput() 
    {
        if (Input.GetMouseButton(0))
        {
            ProcessTouchPosition(Input.mousePosition);
        }
    }

    void FixedUpdate() 
    {
        UpdatePoints();
        for (int i = 0; i < NumIterations; i++)
        {
            UpdateSticks();
            ConstrainPoints();   
        }
    }

    public void InitPoints() 
    {
        // float speed = 1;
        // float stepSize = speed * Time.fixedDeltaTime;
        // AddPoint(new Vector3(0,2.75f,0));
        // AddPoint(new Vector3(1,2f,0));
        //
        // for (int i = 0; i < 10; i++)
        // {
        //     AddPoint(new Vector3(Random.Range(bounds.min.x, bounds.max.x),
        //         Random.Range(bounds.min.y, bounds.max.y),
        //         Random.Range(bounds.min.z, bounds.max.z)));
        // }
        //
        // AddStick(0,1);
        //
        // for (int i = 0; i < 5; i++)
        // {
        //     int i0 = Random.Range(0, points.Count);
        //     int i1 = Random.Range(0, points.Count);
        //     while (i0 == i1)
        //     {
        //         i1 = Random.Range(0, points.Count);
        //     }
        //     AddStick(i0, i1);
        // }

        // AddCloth(ClothPosition);

        // InitFlag();

        InitPinnedGrid();
    }

    private void InitPinnedGrid()
    {
        wind = new Vector3(0, 0, 0);
        NoiseStrength = 0;
        Vector3 startPos = new Vector3(-PinnedGridWidth / 2f, 1, 0);
        Vector3 endPos = new Vector3(PinnedGridWidth / 2f, 1 + PinnedGridHeight, 0);
        int pointStart = points.Count;
        AddGrid(startPos, endPos, out Vector2Int pointsSize);

        for (int x = 0; x < pointsSize.x; x+=PinnedGridPointsPerPin)
        {
            points[pointStart + x * pointsSize.y].Pin();
            points[pointStart + x * pointsSize.y + pointsSize.y - 1].Pin();   
        }

        for (int y = 0; y < pointsSize.y; y += PinnedGridPointsPerPin)
        {
            points[pointStart + y].Pin();
            points[pointStart + y + (pointsSize.x - 1) * pointsSize.y].Pin();
        }
        
        // Make sure the corners get pinned
        points[pointStart + pointsSize.y - 1].Pin();
        points[pointStart + (pointsSize.x - 1) * pointsSize.y].Pin();
        points[pointStart + (pointsSize.x - 1) * pointsSize.y + pointsSize.y - 1].Pin();
    }

    private void AddGrid(Vector3 startPos, Vector3 endPos, out Vector2Int pointsSize)
    {
        Vector3 delta = endPos - startPos;
        
        int indexStart = points.Count;
        int pointsAlongX = Mathf.CeilToInt(Mathf.Abs(delta.x) * PointsPerUnit);
        int pointsAlongY = Mathf.CeilToInt(Mathf.Abs(delta.y) * PointsPerUnit);
        float stepSizeX = delta.x / (pointsAlongX - 1);
        float stepSizeY = delta.y / (pointsAlongY - 1);
        for (int x = 0; x < pointsAlongX; x++)
        {
            float xPos = startPos.x + x * stepSizeX;
            for (int y = 0; y < pointsAlongY; y++)
            {
                Vector3 newPos = new Vector3(xPos, startPos.y + y * stepSizeY, startPos.z);
                AddPoint(newPos);
            }
        }

        pointsSize = new Vector2Int(pointsAlongX, pointsAlongY);
        
        for (int x = 0; x < pointsAlongX - 1; x++)
        {
            int colStart = indexStart + pointsAlongY * x;
            for (int y = 0; y < pointsAlongY - 1; y++)
            {
                int currIndex = colStart + y;
                int bot = colStart + y + 1;
                int right = indexStart + pointsAlongY * (x + 1) + y;
                int botRight = right + 1;
        
                AddStick(currIndex, right);
                AddStick(currIndex, bot);
        
                if (x == pointsAlongX - 2)
                    AddStick(right, botRight);
                if(y == pointsAlongY - 2) 
                    AddStick(bot, botRight);
            }
        }
    }
    
    private void InitFlag()
    {
        wind = new Vector3(1, 0, 0);
        Vector3 startPos = new Vector3(-FlagWidth / 2f, FlagPoleHeight, 0);
        Vector3 endPos = new Vector3(FlagWidth / 2f, startPos.y - FlagHeight, 0);
        int pointStart = points.Count;
        AddGrid(startPos, endPos, out Vector2Int pointsSize);

        for (int y = 0; y < pointsSize.y; y++)
        {
            int index = pointStart + y;
            points[index].pinned = true;
        }
    }
    
    private void AddCloth(Vector3 topClothPos)
    {
        wind = Vector3.zero;
        NoiseStrength = 0;
        
        int indexStart = points.Count;
        Vector3 startCloth = topClothPos - new Vector3(ClothWidth / 2f, 0, 0);
        int pointsAlongX = Mathf.CeilToInt(ClothWidth * PointsPerUnit);
        int pointsAlongY = Mathf.CeilToInt(ClothHeight * PointsPerUnit);
        for (int x = 0; x < pointsAlongX; x++)
        {
            float xPos = startCloth.x + x * ClothWidth / pointsAlongX;
            for (int y = 0; y < pointsAlongY; y++)
            {
                Vector3 newPos = new Vector3(xPos, startCloth.y - y * ClothHeight / pointsAlongY, startCloth.z);
                AddPoint(newPos, y == 0);
            }
        }

        for (int x = 0; x < pointsAlongX - 1; x++)
        {
            int colStart = indexStart + pointsAlongY * x;
            for (int y = 0; y < pointsAlongY - 1; y++)
            {
                int currIndex = colStart + y;
                int bot = colStart + y + 1;
                int right = indexStart + pointsAlongY * (x + 1) + y;
                int botRight = right + 1;

                AddStick(currIndex, right);
                AddStick(currIndex, bot);

                if (x == pointsAlongX - 2)
                    AddStick(right, botRight);
                if(y == pointsAlongY - 2) 
                    AddStick(bot, botRight);
            }
        }
    }

    private void AddPoint(Vector3 pos, bool pinned = false)
    {
        GameObject go = Instantiate(pointPrefab, transform);
        var renderer = go.GetComponent<Renderer>();
        points.Add(new Point() {
            pos = pos,
            oldPos = pos,
            visual = renderer,
            pinned = pinned,
        });
    }

    private void AddStick(int index0, int index1) 
    {
        AddStick(points[index0], points[index1]);
    } 

    private void AddStick(Point p0, Point p1) 
    {
        sticks.Add(new Stick() {
            p0 = p0,
            p1 = p1,
            length = (p0.pos - p1.pos).magnitude,
            visual = Instantiate(stickPrefab, transform),
        });
    }

    public void UpdatePoints() 
    {
        foreach(Point p in points)
        {
            if (p.pinned)
                continue;
            
            Vector3 v = (p.pos - p.oldPos) * friction;
            p.oldPos = p.pos;
            p.pos += v;
            Vector3 noise = new Vector3(Random.Range(-1, 1), Random.Range(-1, 1), Random.Range(-1, 1)).normalized
                            * NoiseStrength;
            float windStrength = Mathf.Lerp(WindowNoiseRange.x, WindowNoiseRange.y, Mathf.Clamp01(Mathf.PerlinNoise(0, Time.time * WindNoiseFrequency)));
            p.pos += (gravity + wind * windStrength + noise) * Time.fixedDeltaTime;
        }
    }

    public void ConstrainPoints()
    {
        foreach (Point p in points)
        {
            if (p.pinned)
                continue;
            
            Vector3 v = (p.pos - p.oldPos) * friction;
            if (p.pos.x > bounds.max.x)
            {
                p.pos.x = bounds.max.x;
                p.oldPos.x = p.pos.x + v.x * bounce;
            }
            else if (p.pos.x < bounds.min.x)
            {
                p.pos.x = bounds.min.x;
                p.oldPos.x = p.pos.x + v.x * bounce;
            }

            if (p.pos.y > bounds.max.y)
            {
                p.pos.y = bounds.max.y;
                p.oldPos.y = p.pos.y + v.y * bounce;
            }
            else if (p.pos.y < bounds.min.y)
            {
                p.pos.y = bounds.min.y;
                p.oldPos.y = p.pos.y + v.y * bounce;
            }

            if (p.pos.z > bounds.max.z)
            {
                p.pos.z = bounds.max.z;
                p.oldPos.z = p.pos.z + v.z * bounce;
            }
            else if (p.pos.z < bounds.min.z)
            {
                p.pos.z = bounds.min.z;
                p.oldPos.z = p.pos.z + v.z * bounce;
            }
        }
    }
    
    public void UpdateSticks() 
    {
        foreach(Stick stick in sticks)
        {
            Vector3 delta = stick.p1.pos - stick.p0.pos;
            float distance = delta.magnitude;
            float diff = stick.length - distance;
            float percent = diff / distance / 2f;
            Vector3 offset = delta * percent;

            if(!stick.p0.pinned)
                stick.p0.pos -= offset;
            if(!stick.p1.pinned)
                stick.p1.pos += offset;

        }
    }

    public void UpdateRenderPos() 
    {
        foreach(Point p in points)
        {
            p.visual.transform.position = p.pos;
            if (p.pinned)
                p.oldPos = p.pos;
            
            Vector3 vel = p.Velocity;
            
            // lerped = lerped.normalized * .5f;
            // Color col = new Color(lerped.x, lerped.y, lerped.z, 1);
            
            // float len = vel.magnitude;
            // len = Remap(len, VelocityRangeInput.x, VelocityRangeInput.y, VelocityRangeOutput.x, VelocityRangeOutput.y);
            // Color col = new Color(len, len, len, 1);
            vel.x = Mathf.Abs(vel.x);
            vel.y = Mathf.Abs(vel.y);
            vel.z = Mathf.Abs(vel.z);
            Color col = new Color(Remap(vel.x, VelocityRangeInput.x, VelocityRangeInput.y, VelocityRangeOutput.x, VelocityRangeOutput.y),
                Remap(vel.y, VelocityRangeInput.x, VelocityRangeInput.y, VelocityRangeOutput.x, VelocityRangeOutput.y),
                Remap(vel.z, VelocityRangeInput.x, VelocityRangeInput.y, VelocityRangeOutput.x, VelocityRangeOutput.y));
            Color prevColor = p.visual.material.GetColor("_BaseColor");
            p.visual.material.SetColor("_BaseColor", Color.Lerp(prevColor, col, Time.deltaTime * ColorChangeSpeed));
        }

        foreach(Stick s in sticks) 
        {
            var r = s.visual;
            r.positionCount = 2;
            r.SetPositions(new Vector3[] {
                s.p0.pos,
                s.p1.pos,
            });

            Vector3 lerped = Vector3.Lerp(s.p0.Velocity, s.p1.Velocity, .5f);
            
            // lerped = lerped.normalized * .5f;
            // Color col = new Color(lerped.x, lerped.y, lerped.z, 1);
            
            float len = lerped.magnitude;
            len = Remap(len, VelocityRangeInput.x, VelocityRangeInput.y, VelocityRangeOutput.x, VelocityRangeOutput.y);
            Color col = new Color(len, len, len, 1);
            Color prevColor = r.material.GetColor("_BaseColor");
            r.material.SetColor("_BaseColor", Color.Lerp(prevColor, col, Time.deltaTime * ColorChangeSpeed));
        }
    }

    public float Remap(float val, float min, float max, float newMin, float newMax)
    {
        val = Mathf.Clamp(val, min, max);
        float t = (val - min) / (max - min);
        return Mathf.Lerp(newMin, newMax, t);
    }
}
