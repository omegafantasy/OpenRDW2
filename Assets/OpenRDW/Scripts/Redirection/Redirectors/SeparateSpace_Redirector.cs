using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SeparateSpace_Redirector : Redirector
{
    const float SQUARELENGTH = 0.5f;
    public List<Tuple<int, float, float>> collisionParams;
    public Tuple<float, float> collisionTimeRange;// the time range with current dir
    public Tuple<float, float> resetTimeRange;// the time range with any dir
    public float timeToWayPoint;// time needed to get to current waypoint
    public Tuple<int, float, float> redirectParams;// straight/left/right, gr(radius), gt
    public Tuple<Vector2, int, float, float> furthestParams;// dir, redirectParams; it can go the furthest distance
    public List<PositionSquare> positionSquares;
    public bool useRedirectParams = false;

    private void Awake()
    {
        useRedirectParams = false;
    }

    public override void InjectRedirection()
    {
        if (useRedirectParams)
        {
            float radius = redirectParams.Item1 == 1 ? redirectParams.Item2 : -redirectParams.Item2;
            float gt = redirectParams.Item3;
            SetTranslationGain(gt);
            SetCurvature(1 / radius);

            ApplyGains();
        }
    }

    public class PositionSquare
    {
        public SeparateSpace_Redirector redirector;
        public Vector2 from;
        public Vector2 to;
        public Vector2 center;
        public List<Tuple<float, float, Vector2>> list; // mintime, maxtime, dir
        public float safePoint; // value to present the position's safety, smaller means safer
        public float maxDis;
        public PositionSquare(float fromx, float fromy, SeparateSpace_Redirector redirector)
        {
            this.from = new Vector2(fromx, fromy);
            this.to = this.from + new Vector2(SQUARELENGTH, SQUARELENGTH);
            this.center = (this.from + this.to) / 2;
            this.safePoint = 0.0f;
            this.maxDis = 0.0f;
            this.redirector = redirector;
            this.list = new List<Tuple<float, float, Vector2>>();
        }
        public bool InsidePolygon(List<Vector2> polygon)
        {
            int count = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 p = polygon[i];
                Vector2 q = polygon[(i + 1) % polygon.Count];
                if (((p.x <= center.x && center.x <= q.x) || (q.x <= center.x && center.x <= p.x)) && p.y <= center.y)
                {
                    count++;
                }
            }
            if (count % 2 == 1)
            {
                return true;
            }
            return false;
        }

        public void GetTimeRangeList()
        {
            Vector2 unit = new Vector2(1, 0);
            for (int i = 0; i < 30; i++)
            {
                Vector2 nowDir = new Vector2((float)Mathf.Cos(Mathf.PI / 15 * i), (float)Mathf.Sin(Mathf.PI / 15 * i));
                Tuple<float, float> timeRange = redirector.GetTimeRange(redirector.GetCollisionParams(center, nowDir));
                safePoint += 1 / timeRange.Item2;
                maxDis = timeRange.Item2 > maxDis ? timeRange.Item2 : maxDis;
                list.Add(new Tuple<float, float, Vector2>(timeRange.Item1, timeRange.Item2, nowDir));
            }
        }
    }

    public void InitializePositionSquares()
    {
        positionSquares = new List<PositionSquare>();
        var trackingSpacePoints = globalConfiguration.physicalSpaces[movementManager.physicalSpaceIndex].trackingSpace;
        var obstaclePolygons = globalConfiguration.physicalSpaces[movementManager.physicalSpaceIndex].obstaclePolygons;
        float xmin = 100000f, xmax = -100000f, ymin = 100000f, ymax = -100000f;
        foreach (var c in trackingSpacePoints)
        {
            xmin = c.x < xmin ? c.x : xmin;
            xmax = c.x > xmax ? c.x : xmax;
            ymin = c.y < ymin ? c.y : ymin;
            ymax = c.y > ymax ? c.y : ymax;
        }
        float xbase, ybase, xcount, ycount;

        // TODO: the following config must be modified according to the physical spaces
        // space preprocessing config start
        if (movementManager.avatarId == 0)
        {
            xbase = 0f;
            ybase = 0f;
            xcount = (int)(5f / SQUARELENGTH);
            ycount = (int)(5f / SQUARELENGTH);
        }
        else if (movementManager.avatarId == 1)
        {
            xbase = -7f;
            ybase = -0f;
            xcount = (int)(5f / SQUARELENGTH);
            ycount = (int)(5f / SQUARELENGTH);
        }
        else if (movementManager.avatarId == 2)
        {
            xbase = -7f;
            ybase = -7f;
            xcount = (int)(5f / SQUARELENGTH);
            ycount = (int)(5f / SQUARELENGTH);
        }
        else if (movementManager.avatarId == 3)
        {
            xbase = 0f;
            ybase = -7f;
            xcount = (int)(5f / SQUARELENGTH);
            ycount = (int)(5f / SQUARELENGTH);
        }
        else if (movementManager.avatarId == 4)
        {
            xbase = 7f;
            ybase = 0f;
            xcount = (int)(5f / SQUARELENGTH);
            ycount = (int)(5f / SQUARELENGTH);
        }
        else if (movementManager.avatarId == 5)
        {
            xbase = 7f;
            ybase = -7f;
            xcount = (int)(5f / SQUARELENGTH);
            ycount = (int)(5f / SQUARELENGTH);
        }
        else
        {
            xbase = 0f;
            ybase = 0f;
            xcount = 0;
            ycount = 0;
        }
        // space preprocessing config end

        // System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        // stopWatch.Start();
        for (int i = 0; i < xcount; i++)
        {
            for (int j = 0; j < ycount; j++)
            {
                PositionSquare newSquare = new PositionSquare(xbase + i * SQUARELENGTH, ybase + j * SQUARELENGTH, this);
                bool valid = newSquare.InsidePolygon(trackingSpacePoints);
                foreach (var obstaclePolygon in obstaclePolygons)
                {
                    if (newSquare.InsidePolygon(obstaclePolygon))
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid)
                {
                    newSquare.GetTimeRangeList();
                    positionSquares.Add(newSquare);
                }
            }
        }
        // stopWatch.Stop();
        // long times1 = stopWatch.ElapsedMilliseconds;
        // Debug.Log(movementManager.avatarId + ":" + times1 + "ms");
    }

    public List<Tuple<int, float, float>> GetCollisionParams(Vector2 pos, Vector2 dir)
    { //return: int(straight:0/left:1/right:2), float(curvature radius), float(dis) 
        var realPos = pos;
        var realDir = dir;
        realDir = realDir.normalized;

        List<Tuple<int, float, float>> list = new List<Tuple<int, float, float>>();
        float buffer = globalConfiguration.RESET_TRIGGER_BUFFER;

        //left and right
        var K = 5;
        for (int i = 0; i < K; i++)
        {
            for (int j = 1; j < 3; j++)
            {
                float mindis = 100000f;
                float radius = 0.0f;//radius of the circle
                radius = globalConfiguration.CURVATURE_RADIUS / Mathf.Cos(Mathf.PI / (2 * K) * i);
                // switch (i)
                // {
                //     case 1:
                //         radius = globalConfiguration.CURVATURE_RADIUS;
                //         break;
                //     case 2:
                //         radius = 1.2f * globalConfiguration.CURVATURE_RADIUS;
                //         break;
                //     case 3:
                //         radius = 1.5f * globalConfiguration.CURVATURE_RADIUS;
                //         break;
                //     case 4:
                //         radius = 1.9f * globalConfiguration.CURVATURE_RADIUS;
                //         break;
                //     case 5:
                //         radius = 2.5f * globalConfiguration.CURVATURE_RADIUS;
                //         break;
                //     default:
                //         break;
                // }
                Vector2 rotate_radius = radius * realDir;
                if (j == 1)
                {//left
                    rotate_radius = new Vector2(-rotate_radius.y, rotate_radius.x);
                }
                else
                {//right,clockwise
                    rotate_radius = new Vector2(rotate_radius.y, -rotate_radius.x);
                }
                Vector2 center = realPos + rotate_radius;//center of the circle
                foreach (var polygon in redirectionManager.polygons)
                {
                    for (int k = 0; k < polygon.Count; k++)
                    {
                        var p = polygon[k];
                        var q = polygon[(k + 1) % polygon.Count];
                        Vector2 ortho_dir = new Vector2((p - q).y, (q - p).x).normalized;// point to line, orthogonal to line
                        if (Mathf.Sign(Cross(realPos + ortho_dir * 1000f - q, p - q)) == Mathf.Sign(Cross(realPos - q, p - q)))
                        {
                            ortho_dir = -ortho_dir;
                        }
                        //see a segment as 3 segments
                        float dis1 = CircleCollisionDis(realPos, realDir, p, q, radius, center);
                        mindis = dis1 < mindis ? dis1 : mindis;
                    }
                }
                mindis = (mindis - buffer * 1.5f > 0) ? mindis - buffer * 1.5f : 0;
                list.Add(new Tuple<int, float, float>(j, radius, mindis));//add to the list
            }
        }

        //straight line
        float straight_mindis = 100000f;
        foreach (var polygon in redirectionManager.polygons)
        {
            for (int k = 0; k < polygon.Count; k++)
            {
                var p = polygon[k];
                var q = polygon[(k + 1) % polygon.Count];
                Vector2 ortho_dir = new Vector2((p - q).y, (q - p).x).normalized;// point to line, orthogonal to line

                if (Mathf.Sign(Cross(realPos + ortho_dir * 1000f - q, p - q)) == Mathf.Sign(Cross(realPos - q, p - q)))
                {
                    ortho_dir = -ortho_dir;
                }

                //see a segment as 3 segments
                float dis1 = LineCollisionDis(realPos, realDir, p, q);
                straight_mindis = dis1 < straight_mindis ? dis1 : straight_mindis;
            }
        }
        straight_mindis = straight_mindis > 0 ? straight_mindis : 0;
        list.Add(new Tuple<int, float, float>(0, 100000f, straight_mindis));//add to the list

        foreach (var tuple in list)
        {
            //Debug.Log(tuple);
        }
        //Debug.Log(redirectionManager.currDirReal.x + ":" + redirectionManager.currDirReal.z + ":" + redirectionManager.currPosReal.x + ":" + redirectionManager.currPosReal.z);
        return list;
    }

    public float LineCollisionDis(Vector2 realPos, Vector2 realDir, Vector2 p, Vector2 q)
    {
        Vector2 ortho_dir = new Vector2((p - q).y, (q - p).x).normalized;// recalculate
        if (Mathf.Sign(Cross(realPos + ortho_dir * 1000f - q, p - q)) == Mathf.Sign(Cross(realPos - q, p - q)))
        {
            ortho_dir = -ortho_dir;
        }
        if (Vector2.Dot(ortho_dir, realDir) <= 0)
        {
            return 100000f;
        }
        return (Dis(realPos, p, q) - globalConfiguration.RESET_TRIGGER_BUFFER) / (float)Mathf.Max(Mathf.Acos(Mathf.Abs(Vector2.Dot(realDir, p - q)) / (p - q).magnitude), 0.001f);
    }

    public float CircleCollisionDis(Vector2 realPos, Vector2 realDir, Vector2 p, Vector2 q, float radius, Vector2 center)
    {
        float center_dis = Dis(center, p, q);
        Vector2 ortho_dir = new Vector2((p - q).y, (q - p).x).normalized;// recalculate
        if (Mathf.Sign(Cross(realPos + ortho_dir * 1000f - q, p - q)) == Mathf.Sign(Cross(realPos - q, p - q)))
        {
            ortho_dir = -ortho_dir;
        }
        float collision_dis = 100000f;
        if (center_dis > radius)
        {//not intersect
            return collision_dis;
        }


        Vector2 center_ortho_dir = ortho_dir;
        if (Mathf.Sign(Cross(center + center_ortho_dir * 1000f - q, p - q)) == Mathf.Sign(Cross(center - q, p - q)))
        {
            center_ortho_dir = -center_ortho_dir;
        }

        float half_string_dis = (float)Mathf.Sqrt(radius * radius - center_dis * center_dis);
        //intersect points
        Vector2 point1 = center + center_ortho_dir * center_dis + half_string_dis * (p - q).normalized;
        Vector2 point2 = center + center_ortho_dir * center_dis - half_string_dis * (p - q).normalized;
        if (Mathf.Sign(Cross(p - center, p - q)) == Mathf.Sign(Cross(p - realPos, p - q)))
        {//realPos and center in one side
            if (InLineSection(point1, p, q))
            {// point1 in section
                if (Vector2.Dot(realDir, point1 - center) >= 0)
                {//near intersect
                    float tmp_dis = radius * (float)(Vector2.Angle(realPos - center, point1 - center) / 180 * Mathf.PI);
                    collision_dis = tmp_dis < collision_dis ? tmp_dis : collision_dis;
                }
                else
                {//far intersect
                    float tmp_dis = radius * (float)(Mathf.PI * 2 - Vector2.Angle(realPos - center, point1 - center) / 180 * Mathf.PI);
                    collision_dis = tmp_dis < collision_dis ? tmp_dis : collision_dis;
                }
            }
            if (InLineSection(point2, p, q))
            {// point2 in section
                if (Vector2.Dot(realDir, point2 - center) >= 0)
                {//near intersect
                    float tmp_dis = radius * (float)(Vector2.Angle(realPos - center, point2 - center) / 180 * Mathf.PI);
                    collision_dis = tmp_dis < collision_dis ? tmp_dis : collision_dis;
                }
                else
                {//far intersect
                    float tmp_dis = radius * (float)(Mathf.PI * 2 - Vector2.Angle(realPos - center, point2 - center) / 180 * Mathf.PI);
                    collision_dis = tmp_dis < collision_dis ? tmp_dis : collision_dis;
                }
            }
        }
        else
        {//realPos and center in two sides
            if (Vector2.Dot(point1 - center, realDir) >= 0)
            {//consider point1
                if (InLineSection(point1, p, q))
                {// point1 in section
                    collision_dis = radius * (float)(Vector2.Angle(realPos - center, point1 - center) / 180 * Mathf.PI);
                }
            }
            else
            {//consider point2
                if (InLineSection(point2, p, q))
                {// point2 in section
                    collision_dis = radius * (float)(Vector2.Angle(realPos - center, point2 - center) / 180 * Mathf.PI);
                }
            }
        }
        // if (p.x < 5.2f && p.x > 4.8f && q.x < 5.2f && q.x > 4.8f && radius < 8f && realPos.x > 14 && realPos.y < 1 && realPos.y > 0.5 && realDir.y > 0.99f && realDir.x > 0)
        // {
        //     Debug.Log("here");
        //     Debug.Log(center_dis);
        //     Debug.Log(realPos);
        //     Debug.Log(realDir);
        //     Debug.Log(radius);
        //     Debug.Log(collision_dis);
        // }
        return collision_dis;
    }

    public Tuple<float, float> GetDisRange(List<Tuple<int, float, float>> list)
    {
        float min = 100000f, max = 0.0f;
        foreach (var tuple in list)
        {
            min = tuple.Item3 < min ? tuple.Item3 : min;
            max = tuple.Item3 > max ? tuple.Item3 : max;
        }
        return new Tuple<float, float>(min, max);
    }

    public Tuple<float, float> GetTimeRange(List<Tuple<int, float, float>> list)
    {
        Tuple<float, float> dis_range = GetDisRange(list);
        return new Tuple<float, float>(dis_range.Item1 / globalConfiguration.translationSpeed * globalConfiguration.MIN_TRANS_GAIN, dis_range.Item2 / globalConfiguration.translationSpeed * globalConfiguration.MAX_TRANS_GAIN);
    }

    public Tuple<float, float> GetCircleDisRange(float time)
    {
        float min = time * globalConfiguration.translationSpeed / globalConfiguration.MAX_TRANS_GAIN;
        float radius = globalConfiguration.CURVATURE_RADIUS;
        float angle = min / radius;
        if (angle < Mathf.PI)
        {
            min = (float)(Mathf.Sin(angle / 2) * radius * 2);
        }
        else
        {
            Debug.Log("Too Long for a circle");
        }
        float max = time * globalConfiguration.translationSpeed / globalConfiguration.MIN_TRANS_GAIN;
        return new Tuple<float, float>(min, max);
    }

    public Tuple<int, float, float> GetFurthestParams(List<Tuple<int, float, float>> list)
    {
        float max = 0.0f;
        Tuple<int, float, float> ret = new Tuple<int, float, float>(0, 100000f, 1.0f);
        foreach (var tuple in list)
        {
            if (tuple.Item3 > max)
            {
                max = tuple.Item3;
                ret = tuple;
            }
        }
        return ret;
    }

    public void ResetAndGuideToSafePos()
    {
        Debug.Log(movementManager.avatarId + "ResetAndGuideToSafePos");
        float time = timeToWayPoint;
        Tuple<float, float> disRange = GetCircleDisRange(time);
        Vector2 pos = new Vector2(redirectionManager.currPosReal.x, redirectionManager.currPosReal.z);
        float minSafePoint = 100000f;
        PositionSquare targetPosition = null;
        foreach (var position in positionSquares)
        {
            if ((position.center - pos).magnitude >= disRange.Item1 && (position.center - pos).magnitude <= disRange.Item2 && position.safePoint < minSafePoint)
            { // reachable and valueable
                var reachable = ResetReachable(position.center, pos, time);
                if (reachable.Item1)
                {
                    targetPosition = position;
                    minSafePoint = position.safePoint;
                    ((SeparateSpace_Resetter)redirectionManager.resetter).resetDir = reachable.Item2;
                    redirectParams = new Tuple<int, float, float>(reachable.Item3, reachable.Item4, reachable.Item5);
                }
            }
        }
        useRedirectParams = true;
        ((SeparateSpace_Resetter)redirectionManager.resetter).useResetDir = true;
        if (targetPosition == null)
        {
            ResetAndGuideToFurthest();
        }
    }
    public void GuideToSafePos()
    {
        Debug.Log(movementManager.avatarId + "GuideToSafePos");
        float time = timeToWayPoint;
        Vector2 pos = new Vector2(redirectionManager.currPosReal.x, redirectionManager.currPosReal.z);
        Vector2 dir = Utilities.FlattenedPos2D(Utilities.GetRelativePosition(redirectionManager.targetWaypoint.position, redirectionManager.trackingSpace.transform) - redirectionManager.currPosReal).normalized;
        float minSafePoint = 100000f;
        PositionSquare targetPosition = null;
        foreach (var position in positionSquares)
        {
            var reachable = Reachable(position.center, pos, dir, time);
            if (reachable.Item1 && position.safePoint < minSafePoint)
            { // reachable and valuable
                targetPosition = position;
                redirectParams = new Tuple<int, float, float>(reachable.Item2, reachable.Item3, reachable.Item4);
                minSafePoint = position.safePoint;
            }
        }
        if (targetPosition == null)
        {
            GuideToFurthest();
        }
        useRedirectParams = true;
    }
    public void ResetAndGuideToFurthest()
    {
        Debug.Log(movementManager.avatarId + "ResetAndGuideToFurthest");
        ((SeparateSpace_Resetter)redirectionManager.resetter).resetDir = furthestParams.Item1;

        redirectParams = new Tuple<int, float, float>(furthestParams.Item2, furthestParams.Item3, globalConfiguration.MAX_TRANS_GAIN);
        if (movementManager.avatarId == 0)
        {
            // Debug.Log(resetDir);
            // Debug.Log(furthestParams.Item2);
            // Debug.Log(furthestParams.Item3);
            // Debug.Log(furthestParams.Item4);
        }
        useRedirectParams = true;
        ((SeparateSpace_Resetter)redirectionManager.resetter).useResetDir = true;
    }
    public void GuideToFurthest()
    {
        Debug.Log(movementManager.avatarId + "GuideToFurthest");
        float max = 0.0f;
        foreach (var tuple in collisionParams)
        {
            if (tuple.Item3 > max)
            {
                max = tuple.Item3;
                redirectParams = new Tuple<int, float, float>(tuple.Item1, tuple.Item2, globalConfiguration.MAX_TRANS_GAIN);
            }
        }
        useRedirectParams = true;
    }
    public void ResetAndGuideToMaxTimePos(float time)
    {
        Debug.Log(movementManager.avatarId + "ResetAndGuideToMaxTimePos");
        Tuple<float, float> disRange = GetCircleDisRange(time);
        Vector2 pos = new Vector2(redirectionManager.currPosReal.x, redirectionManager.currPosReal.z);
        float maxDis = 0f;
        PositionSquare targetPosition = null;
        foreach (var position in positionSquares)
        {
            if ((position.center - pos).magnitude >= disRange.Item1 && (position.center - pos).magnitude <= disRange.Item2 && position.maxDis > maxDis)
            { // reachable and valuable
                var reachable = ResetReachable(position.center, pos, time);
                if (reachable.Item1)
                {
                    targetPosition = position;
                    maxDis = position.maxDis;
                    ((SeparateSpace_Resetter)redirectionManager.resetter).resetDir = reachable.Item2;
                    redirectParams = new Tuple<int, float, float>(reachable.Item3, reachable.Item4, reachable.Item5);
                }
            }
        }
        useRedirectParams = true;
        ((SeparateSpace_Resetter)redirectionManager.resetter).useResetDir = true;
        if (targetPosition == null)
        {
            ResetAndGuideToFurthest();
        }
    }
    public void GuideToMaxTimePos(float time)
    {
        Debug.Log(movementManager.avatarId + "GuideToMaxTimePos");
        Vector2 pos = new Vector2(redirectionManager.currPosReal.x, redirectionManager.currPosReal.z);
        Vector2 dir = Utilities.FlattenedPos2D(Utilities.GetRelativePosition(redirectionManager.targetWaypoint.position, redirectionManager.trackingSpace.transform) - redirectionManager.currPosReal).normalized;
        float maxDis = 0f;
        PositionSquare targetPosition = null;
        foreach (var position in positionSquares)
        {
            var reachable = Reachable(position.center, pos, dir, time);
            if (reachable.Item1 && position.maxDis > maxDis)
            { // reachable and valuable
                targetPosition = position;
                redirectParams = new Tuple<int, float, float>(reachable.Item2, reachable.Item3, reachable.Item4);
                maxDis = position.maxDis;
            }
        }
        if (targetPosition == null)
        {
            GuideToFurthest();
        }
        useRedirectParams = true;
    }

    // public Tuple<int, float, float> SelectTime(List<Tuple<int, float, float>> list, float time)
    // {//return: int(straight0/left1/right2),float(curvature gain),float(translation gain)
    //     float dis = globalConfiguration.translationSpeed * time;
    //     list.Sort(new TupleDisComparer());
    //     Tuple<int, float, float> now_op = new Tuple<int, float, float>(0, 100000f, 1.0f);
    //     float now_diff = 100000f;
    //     foreach (var tuple in list)
    //     {
    //         if (tuple.Item3 * globalConfiguration.MIN_TRANS_GAIN <= dis + 0.001f && tuple.Item3 * globalConfiguration.MAX_TRANS_GAIN >= dis - 0.001f)
    //         {
    //             Tuple<int, float, float> op_param = new Tuple<int, float, float>(tuple.Item1, tuple.Item2, tuple.Item3 / dis);
    //             redirectParams = op_param;

    //             return op_param;
    //         }

    //         if (Mathf.Abs(tuple.Item3 * globalConfiguration.MIN_TRANS_GAIN - tuple.Item3) < now_diff)
    //         {
    //             now_op = new Tuple<int, float, float>(tuple.Item1, tuple.Item2, globalConfiguration.MIN_TRANS_GAIN);
    //             now_diff = Mathf.Abs(tuple.Item3 * globalConfiguration.MIN_TRANS_GAIN - tuple.Item3);
    //         }
    //         if (Mathf.Abs(tuple.Item3 * globalConfiguration.MAX_TRANS_GAIN - tuple.Item3) < now_diff)
    //         {
    //             now_op = new Tuple<int, float, float>(tuple.Item1, tuple.Item2, globalConfiguration.MAX_TRANS_GAIN);
    //             now_diff = Mathf.Abs(tuple.Item3 * globalConfiguration.MAX_TRANS_GAIN - tuple.Item3);
    //         }
    //     }
    //     redirectParams = now_op;
    //     return redirectParams;
    // }

    public float GetTimeToWayPoint()
    {
        float time = (Utilities.FlattenedPos2D(redirectionManager.currPos - redirectionManager.targetWaypoint.position).magnitude - globalConfiguration.distanceToWaypointThreshold) / globalConfiguration.translationSpeed;
        time = time > 0 ? time : 0;
        return time;
    }

    public Tuple<Vector2, float> GetOptimalDirection()
    {
        Vector2 curr = new Vector2(redirectionManager.currDirReal.x, redirectionManager.currDirReal.z);
        Vector2 pos = new Vector2(redirectionManager.currPosReal.x, redirectionManager.currPosReal.z);
        Vector2 optimal = curr;
        float max = 0f;
        for (int i = 0; i < 30; i++)
        {
            Vector2 nowDir = new Vector2((float)(Mathf.Cos(Mathf.PI / 15 * i) * curr.x - Mathf.Sin(Mathf.PI / 15 * i) * curr.y), (float)(Mathf.Sin(Mathf.PI / 15 * i) * curr.x + Mathf.Cos(Mathf.PI / 15 * i) * curr.y));//rotation
            if (!IsDirSafe(pos, nowDir))
            {
                continue;
            }
            float nowMax = GetTimeRange(GetCollisionParams(pos, nowDir)).Item2;
            if (nowMax > max)
            {
                optimal = nowDir;
                max = nowMax;
            }
        }
        return new Tuple<Vector2, float>(optimal, max);
    }

    public Tuple<float, float> GetResetTimeRange(Vector2 pos)
    {
        Vector2 unit = new Vector2(1, 0);
        Tuple<int, float, float> furParams = new Tuple<int, float, float>(0, 100000f, 1.0f);
        Vector2 furDir = new Vector2(1.0f, 0);
        float min = 100000f, max = 0;
        for (int i = 0; i < 30; i++)
        {
            Vector2 nowDir = new Vector2((float)(Mathf.Cos(Mathf.PI / 15 * i) * unit.x - Mathf.Sin(Mathf.PI / 15 * i) * unit.y), (float)(Mathf.Sin(Mathf.PI / 15 * i) * unit.x + Mathf.Cos(Mathf.PI / 15 * i) * unit.y));
            if (!IsDirSafe(pos, nowDir))
            {
                continue;
            }
            var coParams = GetCollisionParams(pos, nowDir);
            Tuple<float, float> timeRange = GetTimeRange(coParams);
            if (timeRange.Item2 > max)
            {
                furParams = GetFurthestParams(coParams);
                furDir = nowDir;
            }
            min = timeRange.Item1 < min ? timeRange.Item1 : min;
            max = timeRange.Item2 > max ? timeRange.Item2 : max;
        }
        furthestParams = new Tuple<Vector2, int, float, float>(furDir, furParams.Item1, furParams.Item2, furParams.Item3);
        return new Tuple<float, float>(min, max);
    }

    public bool IsResetValuable()
    {
        if (GetOptimalDirection().Item2 > GetDisRange(collisionParams).Item2 * 1.2f)
        {
            return true;
        }
        return false;
    }

    //utils

    private Tuple<bool, Vector2, int, float, float> ResetReachable(Vector2 target, Vector2 pos, float time)
    {
        Tuple<bool, Vector2, int, float, float> wrong = new Tuple<bool, Vector2, int, float, float>(false, new Vector2(0, 0), 0, 0, 0);
        float dis = (target - pos).magnitude;
        if (time * globalConfiguration.translationSpeed / globalConfiguration.MAX_TRANS_GAIN <= dis)
        { // use line
            Vector2 dir = (target - pos).normalized;
            if (IsDirSafe(pos, dir))
            {
                float mindis = 100000f;
                foreach (var polygon in redirectionManager.polygons)
                {
                    for (int k = 0; k < polygon.Count; k++)
                    {
                        var p = polygon[k];
                        var q = polygon[(k + 1) % polygon.Count];
                        float dis1 = LineCollisionDis(pos, dir, p, q);
                        mindis = dis1 < mindis ? dis1 : mindis;
                    }
                }
                if (mindis < dis)
                {
                    return wrong;
                }
                else
                {
                    float gt = globalConfiguration.translationSpeed / dis * time;
                    return new Tuple<bool, Vector2, int, float, float>(true, dir, 0, 100000, gt);
                }
            }
            else
            {
                return wrong;
            }
        }
        else
        {
            float radius = globalConfiguration.CURVATURE_RADIUS;
            float angle = (float)Mathf.Asin(dis / 2 / radius);
            float curDis = angle * radius * 2;
            Vector2 dir = (target - pos).normalized;
            Vector2 dir1 = Rotate(dir, angle);
            Vector2 dir2 = Rotate(dir, -angle);
            if (IsDirSafe(pos, dir1))
            {
                float mindis = 100000f;
                Vector2 rotate_radius = radius * dir1;
                rotate_radius = new Vector2(-rotate_radius.y, rotate_radius.x);
                Vector2 center = pos + rotate_radius; //center of the circle
                foreach (var polygon in redirectionManager.polygons)
                {
                    for (int k = 0; k < polygon.Count; k++)
                    {
                        var p = polygon[k];
                        var q = polygon[(k + 1) % polygon.Count];
                        float dis1 = CircleCollisionDis(pos, dir1, p, q, radius, center);
                        mindis = dis1 < mindis ? dis1 : mindis;
                    }
                }
                if (mindis < curDis)
                {
                    return wrong;
                }
                else
                {
                    float gt = globalConfiguration.translationSpeed / curDis * time;
                    return new Tuple<bool, Vector2, int, float, float>(true, dir1, 1, radius, gt);
                }
            }
            if (IsDirSafe(pos, dir2))
            {
                float mindis = 100000f;
                Vector2 rotate_radius = radius * dir2;
                rotate_radius = new Vector2(rotate_radius.y, -rotate_radius.x);
                Vector2 center = pos + rotate_radius;//center of the circle
                foreach (var polygon in redirectionManager.polygons)
                {
                    for (int k = 0; k < polygon.Count; k++)
                    {
                        var p = polygon[k];
                        var q = polygon[(k + 1) % polygon.Count];
                        float dis1 = CircleCollisionDis(pos, dir2, p, q, radius, center);
                        mindis = dis1 < mindis ? dis1 : mindis;
                    }
                }
                if (mindis < curDis)
                {
                    return wrong;
                }
                else
                {
                    float gt = globalConfiguration.translationSpeed / curDis * time;
                    return new Tuple<bool, Vector2, int, float, float>(true, dir2, 2, radius, gt);
                }
            }

            return wrong;
        }
    }

    private Vector2 Rotate(Vector2 ori, float angle)
    { // angle is in radian
        return new Vector2((float)(Mathf.Cos(angle) * ori.x - Mathf.Sin(angle) * ori.y), (float)(Mathf.Sin(angle) * ori.x + Mathf.Cos(angle) * ori.y));
    }

    private Tuple<bool, int, float, float> Reachable(Vector2 target, Vector2 pos, Vector2 dir, float time)
    {
        var disRange = GetCircleDisRange(time);
        Vector2 ortho = new Vector2(dir.y, -dir.x).normalized;
        Vector2 diff = target - pos;
        float a = diff.magnitude * diff.magnitude;
        float b = diff.x * ortho.x + diff.y * ortho.y;
        if (a == 0)
        {
            return new Tuple<bool, int, float, float>(false, 0, 0, 0);
        }
        if (b == 0)
        {
            b = 0.0001f;
        }
        float r = a / b / 2;
        if (Mathf.Abs(r) < globalConfiguration.CURVATURE_RADIUS)
        {
            return new Tuple<bool, int, float, float>(false, 0, 0, 0);
        }
        Vector2 center = pos + r * ortho;
        int way;
        if (r > 0)
        { // right
            way = 2;
        }
        else
        { // left
            way = 1;
            r = -r;
        }
        float angle = Vector2.Angle(center - pos, center - target);
        float dis = (float)(angle / 180f * Mathf.PI * r);
        if (dis < disRange.Item1 || dis > disRange.Item2)
        {
            return new Tuple<bool, int, float, float>(false, 0, 0, 0);
        }

        float mindis = 100000f;
        float buffer = globalConfiguration.RESET_TRIGGER_BUFFER;
        foreach (var polygon in redirectionManager.polygons)
        {
            for (int k = 0; k < polygon.Count; k++)
            {
                var p = polygon[k];
                var q = polygon[(k + 1) % polygon.Count];
                Vector2 ortho_dir = new Vector2((p - q).y, (q - p).x).normalized;// point to line, orthogonal to line
                if (Mathf.Sign(Cross(pos + ortho_dir * 1000f - q, p - q)) == Mathf.Sign(Cross(pos - q, p - q)))
                {
                    ortho_dir = -ortho_dir;
                }
                //see a segment as 3 segments
                float dis1 = CircleCollisionDis(pos, dir, p, q, r, center);
                mindis = dis1 < mindis ? dis1 : mindis;
            }
        }
        if (mindis < dis)
        {
            return new Tuple<bool, int, float, float>(false, 0, 0, 0);
        }
        float gt = globalConfiguration.translationSpeed / dis * time;
        return new Tuple<bool, int, float, float>(true, way, r, gt);
    }

    private class TupleDisComparer : IComparer<Tuple<int, float, float>>
    {
        public int Compare(Tuple<int, float, float> p1, Tuple<int, float, float> p2)
        {
            if (p1.Item3 > p2.Item3)
                return -1;
            if (p1.Item3 < p2.Item3)
            {
                return 1;
            }
            return 0;
        }
    }
    private bool InLineSection(Vector2 point, Vector2 a, Vector2 b)
    {// if point in section pq
        float buffer = globalConfiguration.RESET_TRIGGER_BUFFER;
        Vector2 p = a + (a - b).normalized * (buffer);
        Vector2 q = b + (b - a).normalized * (buffer);
        if (Mathf.Abs(p.x - q.x) > Mathf.Abs(p.y - q.y))
        {
            return (point.x <= p.x && point.x >= q.x) || (point.x <= q.x && point.x >= p.x);
        }
        return (point.y <= p.y && point.y >= q.y) || (point.y <= q.y && point.y >= p.y);
    }

    private bool IsDirSafe(Vector2 realPos, Vector2 dir)
    {// if this direction is away from obstacles
        float mindis = 100000f;
        Vector2 nearestPoint = new Vector2(0f, 0f);
        foreach (var polygon in redirectionManager.polygons)
        {
            for (int k = 0; k < polygon.Count; k++)
            {
                var p = polygon[k];
                var q = polygon[(k + 1) % polygon.Count];
                Vector2 nearestPos = Utilities.GetNearestPos(realPos, new List<Vector2> { p, q });
                if ((nearestPos - realPos).magnitude < mindis)
                {
                    mindis = (nearestPos - realPos).magnitude;
                    nearestPoint = nearestPos;
                }
            }
        }

        if (mindis > 0.1f)
        {
            return true;
        }
        else
        {
            if (Vector2.Dot(realPos - nearestPoint, dir) <= 0 || Vector2.Angle(realPos - nearestPoint, dir) >= 80)
            {
                return false;
            }
            return true;
        }
    }
    private float Dis(Vector2 ori, Vector2 a, Vector2 b) //dis from point ori to line ab
    {
        return Mathf.Abs(Cross(a - b, ori - a)) / (a - b).magnitude;
    }
    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }
}
