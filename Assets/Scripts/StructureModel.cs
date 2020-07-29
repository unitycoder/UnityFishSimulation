﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityTools.Common;
using UnityTools.Debuging;
using UnityTools.Debuging.EditorTool;

namespace UnityFishSimulation
{
    #if USE_EDITOR_EXC
    //[ExecuteInEditMode]
    #endif

    public class StructureModel : MonoBehaviour
    {
        [System.Serializable]
        public class MassPoint : Point, INode
        {
            [SerializeField] protected int id;
            [SerializeField] protected float mass;

            //runtime
            [SerializeField] protected float3 force;
            [SerializeField] protected float3 velocity;

            public int Index { get => this.id; set => this.id = value; }
            public float Mass { get => this.mass; }
            public float3 Force { get => this.force; set => this.force = value; }
            public float3 Velocity { get => this.velocity; set => this.velocity = value; }
        }

        [System.Serializable]
        public class Spring: Segment<MassPoint>
        {
            public enum Type
            {
                Cross,
                MuscleFront,
                MuscleMiddle,
                MuscleBack,
                Normal,
            }

            public enum Side
            {
                Left,
                Right,
                None,
            }

            [SerializeField] protected float c = 38;  // elasticity constant
            [SerializeField] protected float k = 0.1f;// viscosity constant
            [SerializeField] protected float lr = 1;   // rest length
            [SerializeField] protected float lc = 1;   // fully contracted length
            [SerializeField] protected float activation = 0;
            [SerializeField] protected Type type = Type.Normal;
            [SerializeField] protected Side side = Side.None;
            public float Activation { get => this.activation; set => this.activation = value; }
            public float CurrentL { get => math.lerp(this.lr, this.lc, this.activation); }
            public float C { get => this.c; }
            public float K { get => this.k; }
            public Type SpringType { get => this.type; }
            public Side SpringSide { get => this.side; }

            public Spring(Type type, Side side, MassPoint from, MassPoint to)
            {
                this.Left = from;
                this.Right = to;

                this.type = type;
                this.side = side;
                this.c = elasticMap[this.type];
                this.k = viscosityMap[this.type];

                this.activation = 0.5f;
                var currentL = math.length(from.Position - to.Position);
                var full = currentL / this.activation;

                var ratio = 0.65f;
                this.lr = full * ratio;
                this.lc = full * (1 - ratio);

                //if (type == Spring.Type.MuscleBack) this.lc = this.lr * 0.3f;
            }


            public override string ToString()
            {
                return "1";
            }
        }

        [System.Serializable]
        public class NormalFace
        {
            [SerializeField] protected float3 normal;
            public List<MassPoint> nodeList = new List<MassPoint>();

            public float3 Normal
            {
                get
                {
                    this.CalNormal();
                    return this.normal;
                }
            }
            public NormalFace(MassPoint p1, MassPoint p2, MassPoint p3, MassPoint p4)
            {
                this.nodeList.Add(p1);
                this.nodeList.Add(p2);
                this.nodeList.Add(p3);
                if (p4 != null) this.nodeList.Add(p4);
            }

            float3 force;
            public void OnGizmos(float length = 1)
            {
                var p1 = this.nodeList[0].Position;
                var p2 = this.nodeList[1].Position;
                var p3 = this.nodeList[2].Position;
                var p4 = this.nodeList.Count > 3 ? this.nodeList[3].Position : float3.zero;
                var mid = (p1+p2+p3+p4)/ this.nodeList.Count;
                using (new GizmosScope(Color.yellow, Matrix4x4.identity))
                {
                    //Gizmos.DrawLine(mid, mid + this.Normal * length);
                }
                using (new GizmosScope(Color.red, Matrix4x4.identity))
                {
                    Gizmos.DrawLine(mid, mid + this.force * length);
                }

                using (new GizmosScope(Color.red, Matrix4x4.identity))
                {
                    //Gizmos.DrawLine(mid, mid + this.vproj * length);
                }
            }

            public void ApplyForceToNode(float mu = 1)
            {
                var area = this.CalArea();
                var velocity = this.CalVelocity();
                var waterVelocity = float3.zero;
                var v = velocity - waterVelocity;
                var n = this.Normal;
                var force = -mu * area * math.length(v) * (math.dot(n, v) * n);
                //force = math.min(0, force);

                var anlge = math.dot(n, math.normalize(force));
                if (anlge > 0) force = 0;
                
                var num = this.nodeList.Count;
                force /= num;

                this.force = force;
                foreach (var node in this.nodeList)
                {
                    node.Force += force;
                }
            }

            protected void CalNormal()
            {
                var p1 = this.nodeList[0].Position;
                var p2 = this.nodeList[1].Position;
                var p3 = this.nodeList[2].Position;
                var v1 = p2 - p1;
                var v2 = p3 - p1;

                this.normal = math.normalize(math.cross(v2, v1));
            }

            protected float Area(float3 p1, float3 p2, float3 p3)
            {
                var v1 = p2 - p1;
                var v2 = p3 - p1;
                var cos = math.dot(math.normalize(v1), math.normalize(v2));
                var sin = math.sqrt(1 - cos * cos);
                return 0.5f * math.length(v1) * math.length(v2) * sin;
            }

            protected float CalArea()
            {
                var p1 = this.nodeList[0].Position;
                var p2 = this.nodeList[1].Position;
                var p3 = this.nodeList[2].Position;
                var p4 = this.nodeList.Count > 3 ? this.nodeList[3].Position : float3.zero;
                var num = this.nodeList.Count;
                if(num == 3)
                {
                    return this.Area(p1, p2, p3);
                }
                else
                {
                    Assert.IsTrue(num == 4);
                    return this.Area(p1, p2, p4) + this.Area(p3, p4, p2);
                }
            }
            protected float3 CalVelocity()
            {
                var v1 = this.nodeList[0].Velocity;
                var v2 = this.nodeList[1].Velocity;
                var v3 = this.nodeList[2].Velocity;
                var v4 = this.nodeList.Count > 3 ? this.nodeList[3].Velocity : float3.zero;
                var num = this.nodeList.Count;

                return (v1 + v2 + v3 + v4) / num;
            }
        }

        protected static Dictionary<Spring.Type, float> elasticMap = new Dictionary<Spring.Type, float>()
        {
            {Spring.Type.Cross , 38f },
            {Spring.Type.MuscleFront, 28f },
            {Spring.Type.MuscleMiddle, 28f },
            {Spring.Type.MuscleBack, 28f },
            {Spring.Type.Normal, 30f },
        };

        protected static Dictionary<Spring.Type, float> viscosityMap = new Dictionary<Spring.Type, float>()
        {
            {Spring.Type.Cross , 0.1f },
            {Spring.Type.MuscleFront, 0.1f },
            {Spring.Type.MuscleMiddle, 0.1f },
            {Spring.Type.MuscleBack, 0.1f },
            {Spring.Type.Normal, 0.1f },
        };
        protected static Dictionary<Spring.Type, Color> springColorMap = new Dictionary<Spring.Type, Color>()
        {
            {Spring.Type.Cross , Color.gray },
            {Spring.Type.MuscleFront, Color.red },
            {Spring.Type.MuscleMiddle, Color.green },
            {Spring.Type.MuscleBack, Color.blue },
            {Spring.Type.Normal, Color.cyan },
        };

        [System.Serializable]
        public class FishModelData
        {
            [SerializeField, Range(0.01f, 1)] protected float damping = 0.05f;
            protected Graph<MassPoint, Spring> fishGraph = new Graph<MassPoint, Spring>(23);
            [SerializeField] protected List<NormalFace> normalFace = new List<NormalFace>();

            public Graph<MassPoint, Spring> FishGraph { get => this.fishGraph; set => this.fishGraph = value; }
            public List<NormalFace> FishNormalFace { get => this.normalFace; }
            public float Damping { get => this.damping; }
        }

        [SerializeField, Range(0.01f, 1)] protected float fluidForceScale = 0.5f;
        [SerializeField] protected FishModelData fishData = new FishModelData();
        [SerializeField] protected List<MassPoint> runtimeList;
        [SerializeField] protected List<Spring> runtimeMuscleList;
        [SerializeField] protected List<Spring> runtimeSpringList;

        protected Graph<MassPoint, Spring> FishGraph { get => this.fishData.FishGraph; }

        protected float3 totalForce;

        public List<Spring> GetSpringByType(List<Spring.Type> types)
        {
            return this.FishGraph.Edges.Where(e => types.Contains(e.SpringType)).ToList();
        }

        protected void Start()
        {
            this.Load();
            this.RefreshRuntimeList();
        }        

        protected void RefreshRuntimeList()
        {
            this.runtimeList = this.FishGraph.Nodes.ToList();
            this.runtimeSpringList = this.FishGraph.Edges.ToList();
            this.runtimeMuscleList = this.GetSpringByType(
                new List<Spring.Type>{
                    Spring.Type.MuscleBack,
                    Spring.Type.MuscleMiddle,
                    Spring.Type.MuscleFront }
                );
        }
        protected void InitNewFishModel()
        {
            this.FishGraph.AdjMatrix.Clean();
            this.InitSprings();
            this.InitNormals();
        }

        protected void InitNormals()
        {
            /*
            this.AddNormalFace(0, 1, 2);
            this.AddNormalFace(0, 2, 3);
            this.AddNormalFace(0, 3, 4);
            this.AddNormalFace(0, 4, 1);*/

            this.AddNormalFace(1, 5, 6, 2);
            this.AddNormalFace(2, 6, 7, 3);
            this.AddNormalFace(3, 7, 8, 4);
            this.AddNormalFace(4, 8, 5, 1);

            this.AddNormalFace(5, 9, 10, 6);
            this.AddNormalFace(6, 10, 11, 7);
            this.AddNormalFace(7, 11, 12, 8);
            this.AddNormalFace(8, 12, 9, 5);

            this.AddNormalFace(9, 13, 14, 10);
            this.AddNormalFace(10, 14, 15, 11);
            this.AddNormalFace(11, 15, 16, 12);
            this.AddNormalFace(12, 16, 13, 9);


            this.AddNormalFace(13, 17, 18, 14);
            this.AddNormalFace(14, 18, 19, 15);
            this.AddNormalFace(15, 19, 20, 16);
            this.AddNormalFace(16, 20, 17, 13);

            this.AddNormalFace(17, 21, 18);
            this.AddNormalFace(18, 21, 22, 19);
            this.AddNormalFace(19, 22, 20);
            this.AddNormalFace(20, 22, 21, 17);
        }

        protected void AddNormalFace(int p1, int p2, int p3, int p4 = -1)
        {
            var nodes = this.FishGraph.Nodes.ToList();
            this.fishData.FishNormalFace.Add(new NormalFace(nodes[p1], nodes[p2], nodes[p3], p4<0?null:nodes[p4]));
        }

        protected void InitSprings()
        {
            this.AddSpring(0, 1, Spring.Type.Normal);
            this.AddSpring(0, 2, Spring.Type.Normal);
            this.AddSpring(0, 3, Spring.Type.Normal);
            this.AddSpring(0, 4, Spring.Type.Normal);

            this.AddSpring(1, 2, Spring.Type.Normal);
            this.AddSpring(1, 3, Spring.Type.Cross);
            this.AddSpring(1, 4, Spring.Type.Normal);
            this.AddSpring(1, 5, Spring.Type.MuscleFront, Spring.Side.Left);//
            this.AddSpring(1, 6, Spring.Type.Cross);
            this.AddSpring(1, 8, Spring.Type.Cross);

            this.AddSpring(2, 3, Spring.Type.Normal);
            this.AddSpring(2, 4, Spring.Type.Cross);
            this.AddSpring(2, 5, Spring.Type.Cross);
            this.AddSpring(2, 6, Spring.Type.MuscleFront, Spring.Side.Right);//
            this.AddSpring(2, 7, Spring.Type.Cross);

            this.AddSpring(3, 4, Spring.Type.Normal);
            this.AddSpring(3, 6, Spring.Type.Cross);
            this.AddSpring(3, 7, Spring.Type.MuscleFront, Spring.Side.Right);//
            this.AddSpring(3, 8, Spring.Type.Cross);

            this.AddSpring(4, 5, Spring.Type.Cross);
            this.AddSpring(4, 7, Spring.Type.Cross);
            this.AddSpring(4, 8, Spring.Type.MuscleFront, Spring.Side.Left);//
            //---------------------------------

            this.AddSpring(5, 6, Spring.Type.Normal);
            this.AddSpring(5, 7, Spring.Type.Cross);
            this.AddSpring(5, 8, Spring.Type.Normal);
            this.AddSpring(5, 9, Spring.Type.MuscleMiddle, Spring.Side.Left);
            this.AddSpring(5, 10, Spring.Type.Cross);
            this.AddSpring(5, 12, Spring.Type.Cross);

            this.AddSpring(6, 7, Spring.Type.Normal);
            this.AddSpring(6, 8, Spring.Type.Cross);
            this.AddSpring(6, 9, Spring.Type.Cross);
            this.AddSpring(6, 10, Spring.Type.MuscleMiddle, Spring.Side.Right);
            this.AddSpring(6, 11, Spring.Type.Cross);

            this.AddSpring(7, 8, Spring.Type.Normal);
            this.AddSpring(7, 10, Spring.Type.Cross);
            this.AddSpring(7, 11, Spring.Type.MuscleMiddle, Spring.Side.Right);
            this.AddSpring(7, 12, Spring.Type.Cross);

            this.AddSpring(8, 9, Spring.Type.Cross);
            this.AddSpring(8, 11, Spring.Type.Cross);
            this.AddSpring(8, 12, Spring.Type.MuscleMiddle, Spring.Side.Left);
            //---------------------------------

            this.AddSpring(9, 10, Spring.Type.Normal);
            this.AddSpring(9, 11, Spring.Type.Cross);
            this.AddSpring(9, 12, Spring.Type.Normal);
            this.AddSpring(9, 13, Spring.Type.MuscleBack, Spring.Side.Left);
            this.AddSpring(9, 14, Spring.Type.Cross);
            this.AddSpring(9, 16, Spring.Type.Cross);

            this.AddSpring(10, 11, Spring.Type.Normal);
            this.AddSpring(10, 12, Spring.Type.Cross);
            this.AddSpring(10, 13, Spring.Type.Cross);
            this.AddSpring(10, 14, Spring.Type.MuscleBack, Spring.Side.Right);
            this.AddSpring(10, 15, Spring.Type.Cross);

            this.AddSpring(11, 12, Spring.Type.Normal);
            this.AddSpring(11, 14, Spring.Type.Cross);
            this.AddSpring(11, 15, Spring.Type.MuscleBack, Spring.Side.Right);
            this.AddSpring(11, 16, Spring.Type.Cross);

            this.AddSpring(12, 13, Spring.Type.Cross);
            this.AddSpring(12, 15, Spring.Type.Cross);
            this.AddSpring(12, 16, Spring.Type.MuscleBack, Spring.Side.Left);
            //--------------------------------

            this.AddSpring(13, 14, Spring.Type.Normal);
            this.AddSpring(13, 15, Spring.Type.Cross);
            this.AddSpring(13, 16, Spring.Type.Normal);
            this.AddSpring(13, 17, Spring.Type.Normal);
            this.AddSpring(13, 18, Spring.Type.Cross);
            this.AddSpring(13, 20, Spring.Type.Cross);

            this.AddSpring(14, 15, Spring.Type.Normal);
            this.AddSpring(14, 16, Spring.Type.Cross);
            this.AddSpring(14, 17, Spring.Type.Cross);
            this.AddSpring(14, 18, Spring.Type.Normal);
            this.AddSpring(14, 19, Spring.Type.Cross);

            this.AddSpring(15, 16, Spring.Type.Normal);
            this.AddSpring(15, 18, Spring.Type.Cross);
            this.AddSpring(15, 19, Spring.Type.Normal);
            this.AddSpring(15, 20, Spring.Type.Cross);

            this.AddSpring(16, 17, Spring.Type.Cross);
            this.AddSpring(16, 19, Spring.Type.Cross);
            this.AddSpring(16, 20, Spring.Type.Normal);
            //---------------------------------

            this.AddSpring(17, 18, Spring.Type.Normal);
            this.AddSpring(17, 19, Spring.Type.Cross);
            this.AddSpring(17, 20, Spring.Type.Normal);
            this.AddSpring(17, 21, Spring.Type.Normal);
            this.AddSpring(17, 22, Spring.Type.Cross);

            this.AddSpring(18, 19, Spring.Type.Normal);
            this.AddSpring(18, 20, Spring.Type.Cross);
            this.AddSpring(18, 21, Spring.Type.Normal);
            this.AddSpring(18, 22, Spring.Type.Cross);

            this.AddSpring(19, 20, Spring.Type.Normal);
            this.AddSpring(19, 21, Spring.Type.Cross);
            this.AddSpring(19, 22, Spring.Type.Normal);

            this.AddSpring(20, 21, Spring.Type.Cross);
            this.AddSpring(20, 22, Spring.Type.Normal);

            this.AddSpring(21, 22, Spring.Type.Normal);

        }

        protected void AddSpring(int from, int to, Spring.Type type, Spring.Side side = Spring.Side.None)
        {
            var nodes = this.FishGraph.Nodes.ToList();
            var s = new Spring(type, side, nodes[from], nodes[to]);

            this.FishGraph.AddEdge(from, to, s);
        }

        protected void Update()
        {
            if(Input.GetKeyDown(KeyCode.S))
            {
                this.Save();
            }
            if(Input.GetKeyDown(KeyCode.L))
            {
                this.Load();
                this.RefreshRuntimeList();
            }

            //if(Input.GetKey(KeyCode.G))
            {
                this.Step();
            }

            if (Input.GetKey(KeyCode.P))
            {
                foreach (var value in Enumerable.Range(1, 500))
                {
                    this.StepMartix();
                }
            }
        }

        protected void Save()
        {
            var path = System.IO.Path.Combine(Application.streamingAssetsPath, "fish.model");
            FileTool.Write(path, this.fishData);
            LogTool.Log("Saved " + path);
        }
        protected void Load()
        {
            var path = System.IO.Path.Combine(Application.streamingAssetsPath, "fish.model");
            this.fishData = FileTool.Read<FishModelData>(path);
            LogTool.Log("Loaded " + path);
        }

        protected void OnDrawGizmos()
        {
            if(this.FishGraph != null)
            {
                foreach( var edge in this.FishGraph.Edges)
                {
                    using (new GizmosScope(springColorMap[edge.SpringType], Matrix4x4.identity))
                    {
                        edge.OnGizmos();
                    }
                }
            }
            foreach (var n in this.FishGraph.Nodes)
            {
                n.OnGizmos(50 * Unit.WorldMMToUnityUnit);
                Gizmos.DrawLine(n.Position, n.Position + n.Velocity);
            }

            foreach (var n in this.fishData.FishNormalFace) n.OnGizmos(200 * Unit.WorldMMToUnityUnit);

            Gizmos.DrawLine(Vector3.zero, this.totalForce);
        }
        protected void Step()
        {
            foreach (var value in Enumerable.Range(1, 10))
            {
                var dt = 0.005f;
                foreach (var n in this.FishGraph.Nodes)
                {
                    var force = this.GetSpringForce(n);
                    n.Force = force;
                }

                this.ApplyFluidForce();

                this.totalForce = 0;
                foreach (var n in this.FishGraph.Nodes)
                {
                    var newVelocity = n.Velocity + (n.Force / n.Mass) * dt;
                    n.Force += -this.fishData.Damping * newVelocity;

                    n.Velocity += (n.Force / n.Mass) * dt;
                    n.Position += n.Velocity * dt;

                    this.totalForce += n.Force;
                }
            }
        }

        protected void StepMartix()
        {
            foreach (var n in this.FishGraph.Nodes)
            {
                n.Force = 0;
            }
            this.ApplyFluidForce();


            var dt = 0.055f;
            //var na = 7;

            var dim = this.FishGraph.AdjMatrix.Size;
            var nodes = this.FishGraph.Nodes.ToList();

            var At = new Matrix<float3>(dim.x, dim.y);
            var Gt = new Matrix<float3>(dim.x, 1);

            foreach (var s_ij in this.runtimeSpringList)
            {
                Assert.IsNotNull(s_ij);

                var ni = s_ij.Left;
                var nj = s_ij.Right;
                var i = ni.Index;
                var j = nj.Index;

                var n_ij = GetN(ni, nj, s_ij.C, s_ij.K, s_ij.CurrentL);
                var r_ij = nj.Position - ni.Position;

                At[i, i] = At[i, i] + n_ij * dt;
                At[j, j] = At[j, j] + n_ij * dt;

                At[i, j] = At[j, i] = -n_ij * dt;

                Gt[i, 0] = Gt[i, 0] + n_ij * r_ij;
                Gt[j, 0] = Gt[j, 0] - n_ij * r_ij;
            }
        
            

            for (var i = 0; i < nodes.Count; ++i)
            {
                var mi = nodes[i].Mass;
                var fi = nodes[i].Force;
                var vi = nodes[i].Velocity;
                At[i, i] = At[i, i] + mi / dt;
                Gt[i, 0] = Gt[i, 0] + fi + (mi / dt) * vi;
            }


            var L = new Matrix<float3>(dim.x, dim.y);
            var D = new Matrix<float3>(dim.x, dim.y);
            var LT = new Matrix<float3>(dim.x, dim.y);
            for (var i = 0; i < dim.x; ++i)
            {
                for (var j = 0; j < dim.y; ++j)
                {
                    if (i >= j) L[i, j] = At[i, j];
                    if (i == j) D[i, j] = At[i, j];
                    if (i <= j) LT[i, j] = At[i, j];
                }
            }

            //this.Print(L, "L", true);
            //this.Print(D, "D", true);
            //this.Print(LT, "LT", true);

            var Q = this.SolverFS(L, Gt);
            //this.Print(Q, "Q", true);
            //D-1Q
            for (var i = 0; i < dim.x; ++i)
            {
                Q[i, 0] *= 1f / D[i, i];
            }
            //this.Print(Q, "Q_1", true);

            var X_dot = SolverBS(LT, Q);

            //this.Print(X_dot, "X_Velocity", true);

            foreach (var n in this.FishGraph.Nodes)
            {
                n.Velocity = X_dot[n.Index,0];
                n.Position += n.Velocity * dt;
            }

        }
        protected Matrix<float3> SolverFS(Matrix<float3> L, Matrix<float3> b)
        {
            var dim = L.Size;
            Assert.IsTrue(dim.x == b.Size.x);

            //Ly = b
            var y = new Matrix<float3>(dim.x, 1);

            var m = dim.x;
            for (var i = 0; i < m; ++i)
            {
                var sum = float3.zero;
                for (var j = 0; j < m - 1; ++j)
                {
                    sum += L[i, j] * y[j, 0];
                }
                y[i, 0] = (b[i, 0] + sum) / L[i, i];
            }

            return y;
        }
        protected Matrix<float3> SolverBS(Matrix<float3> U, Matrix<float3> y)
        {
            var dim = U.Size;
            Assert.IsTrue(dim.x == y.Size.x);

            //Ux = y
            var x = new Matrix<float3>(dim.x, 1);

            var n = dim.x;
            for(var i = 0; i < n; ++i)
            {
                var sum = float3.zero;
                for (var j = i; j < n; ++j)
                {
                    sum += U[i, j] * x[j, 0]; 
                }
                x[i, 0] = (y[i,0] - sum)/ U[i, i];
            }

            return x;
        }

        void Print(Matrix<float3> mat, string name, bool value = false)
        {
            var dim = mat.Size;

            var csv = "";
            for (var i = 0; i < dim.x; ++i)
            {
                for (var j = 0; j < dim.y; ++j)
                {
                    var num = (math.length(mat[i, j]) != 0) ? (value ? mat[i, j].ToString():"1") : " ";
                    //if (i > j) num = " ";
                    csv += num + ",";
                }
                csv += "\n";
            }

            var path = System.IO.Path.Combine(Application.streamingAssetsPath, name+".csv");
            System.IO.File.WriteAllText(path, csv);
        }

        /*protected float3 GetB( i)
        {
            var B = i.
        }*/
        protected float3 GetN(MassPoint i, MassPoint j, float c, float k, float l)
        {
            var r = j.Position - i.Position;
            var r_ij = math.length(r);

            var e_ij = r_ij - l;

            var u_ij = j.Velocity - i.Velocity;
            var r_dot = math.dot(u_ij, r) / r_ij;

            var n_ij = ((c * e_ij) + (k * r_dot)) / r_ij;

            return n_ij;
        }
        protected float3 GetSpringForce(MassPoint i)
        {
            var neighbors = this.FishGraph.GetNeighborsNode(i);
            var ret = float3.zero;

            foreach(var j in neighbors)
            {
                var r = j.Position - i.Position;
                var r_ij = math.length(r);
                var s_ij = this.FishGraph.GetEdge(i, j);
                Assert.IsNotNull(s_ij);

                var e_ij = r_ij - s_ij.CurrentL;

                var u_ij = j.Velocity - i.Velocity;
                var r_dot = (u_ij * r) / r_ij;

                var force_ij = (((s_ij.C * e_ij) + (s_ij.K * r_dot))/ r_ij) * r;
                ret += force_ij;
            }

            return ret;
        }


        protected void ApplyFluidForce()
        {
            foreach(var face in this.fishData.FishNormalFace)
            {
                face.ApplyForceToNode(this.fluidForceScale);
            }
        }

    }
}
