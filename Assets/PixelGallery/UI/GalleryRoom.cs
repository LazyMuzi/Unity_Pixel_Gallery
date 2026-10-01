using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelGallery
{
    public sealed partial class GalleryRoom : MonoBehaviour
    {
        const int RoomLayer = 30;
        sealed class Visitor
        {
            public Transform Root, Body, HeadPose, Badge, LeftLeg, RightLeg, LeftArm, RightArm;
            public MeshRenderer BadgeRenderer;
            public System.Random Random;
            public Vector3 Destination;
            public float VisitAt, StopUntil, WalkSpeed, ReactAt;
            public bool Reacted;
            public int State, ArtIndex, Emotion;
            public float NextVisitAt;
            public bool Evaluated;
        }
        readonly List<Material> materials = new List<Material>();
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly List<Visitor> visitors = new List<Visitor>();
        readonly Material[] galleryArt = new Material[8];
        readonly Texture[] galleryOriginal = new Texture[8];
        readonly Texture2D[] collectionTextures = new Texture2D[3];
        readonly int[] collectionSlots = { 0,4,6 };
        readonly Material[] emoji = new Material[5];
        readonly Material[] clothes = new Material[5];
        Material painting, litTemplate, unlitTemplate, skin, shoes;
        Camera roomCamera;
        RenderTexture view;
        ShowRecord show;
        float time;
        readonly List<Vector3> artPositions = new List<Vector3>();
        readonly List<Vector3> viewingPositions = new List<Vector3>();
        public int OtherArtworkVisits { get; private set; }
        public RenderTexture View => view;
        public int VisitorCount => visitors.Count;
        public int EvaluatedCount { get; private set; }
        public int Likes { get; private set; }
        public int LastEvaluatedIndex { get; private set; } = -1;
        public int RegularVisitStage { get; private set; }
        int regularIndex = -1;
        public Texture ArtworkTexture => painting.mainTexture;

        public void Initialize(Material lit, Material unlit)
        {
            litTemplate = lit; unlitTemplate = unlit;
            transform.position = new Vector3(1000,0,1000);
            var wall = Material("Charcoal",new Color32(52,45,42,255));
            var floor = Material("WalnutFloor",new Color32(139,100,66,255));
            floor.mainTexture = FloorTexture(); floor.mainTextureScale = new Vector2(7,6);
            var wood = Material("DarkWood",new Color32(65,44,31,255));
            var trim = Material("Iron",new Color32(28,29,31,255));
            var gold = Material("Brass",new Color32(192,136,59,255));
            Shape("Floor",transform,PrimitiveType.Cube,new Vector3(0,-.12f,0),new Vector3(14,.24f,12),floor);
            Shape("BackWall",transform,PrimitiveType.Cube,new Vector3(0,2.4f,5),new Vector3(14,4.8f,.3f),wall);
            Shape("RightWall",transform,PrimitiveType.Cube,new Vector3(7,2.4f,0),new Vector3(.3f,4.8f,10),wall);
            Shape("Cornice",transform,PrimitiveType.Cube,new Vector3(0,4.65f,4.76f),new Vector3(14,.18f,.2f),trim);
            Shape("Cornice",transform,PrimitiveType.Cube,new Vector3(6.76f,4.65f,0),new Vector3(.2f,.18f,10),trim);
            for (int i=0;i<5;i++)
            {
                var position = new Vector3((i-2)*2.5f,2.65f,4.72f);
                AddArtwork(i,position,0,i == 2 ? 2.15f : 1.8f,wood,gold,trim);
                Shape("WallPanel",transform,PrimitiveType.Cube,new Vector3(position.x-1.22f,2.3f,4.76f),new Vector3(.12f,4.6f,.1f),trim);
            }
            for (int i=5;i<8;i++) AddArtwork(i,new Vector3(6.72f,2.65f,(i-6)*2.85f),90,1.95f,wood,gold,trim);
            clothes[0] = Material("Sage",new Color32(92,154,113,255));
            clothes[1] = Material("Terracotta",new Color32(190,97,77,255));
            clothes[2] = Material("Indigo",new Color32(84,119,164,255));
            clothes[3] = Material("Ochre",new Color32(193,158,77,255));
            clothes[4] = Material("Plum",new Color32(132,90,139,255));
            skin = Material("Skin",new Color32(226,184,142,255));
            shoes = Material("Shoes",new Color32(37,33,31,255));
            for (int i=0;i<5;i++)
            {
                emoji[i] = Material("Emoji"+i,Color.white,true); emoji[i].mainTexture = EmojiTexture(i);
                emoji[i].SetFloat("_AlphaClip",1); emoji[i].SetFloat("_Cutoff",.5f);
                emoji[i].EnableKeyword("_ALPHATEST_ON"); emoji[i].renderQueue = (int)RenderQueue.AlphaTest;
            }
            var cameraObject = new GameObject("GalleryCamera",typeof(Camera)); cameraObject.transform.SetParent(transform,false);
            roomCamera = cameraObject.GetComponent<Camera>(); roomCamera.orthographic = true; roomCamera.orthographicSize = 8.5f;
            cameraObject.transform.localPosition = new Vector3(-10,11,-14);
            cameraObject.transform.LookAt(transform.TransformPoint(new Vector3(0,1.4f,0)));
            roomCamera.cullingMask = 1 << RoomLayer; roomCamera.clearFlags = CameraClearFlags.SolidColor;
            roomCamera.backgroundColor = new Color32(25,29,32,255); roomCamera.nearClipPlane = .1f; roomCamera.farClipPlane = 40;
            view = new RenderTexture(1600,900,24) { name = "GalleryView", antiAliasing = 2 }; view.Create(); roomCamera.targetTexture = view;
            var lightObject = new GameObject("GalleryLight",typeof(Light)); lightObject.transform.SetParent(transform,false);
            var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = .25f;
            light.color = new Color(1,.94f,.83f); light.cullingMask = 1 << RoomLayer; light.shadows = LightShadows.Soft;
            lightObject.transform.localRotation = Quaternion.Euler(45,-30,0);
            for (int side=-1;side<=1;side+=2)
            {
                Shape("Bench",transform,PrimitiveType.Cube,new Vector3(side*5.65f,.6f,-3.5f),new Vector3(1.5f,.18f,.55f),wood);
                for (int leg=-1;leg<=1;leg+=2) Shape("BenchLeg",transform,PrimitiveType.Cube,new Vector3(side*5.65f+leg*.55f,.27f,-3.5f),new Vector3(.12f,.54f,.4f),trim);
                Shape("PlantPot",transform,PrimitiveType.Cube,new Vector3(side*6.1f,.35f,3.4f),new Vector3(.65f,.7f,.65f),wood);
                Shape("Plant",transform,PrimitiveType.Sphere,new Vector3(side*6.1f,1.2f,3.4f),new Vector3(.9f,1.5f,.9f),clothes[0]);
            }
            var marble = Material("Marble",new Color32(202,194,174,255));
            Shape("SculpturePlinth",transform,PrimitiveType.Cube,new Vector3(-5.7f,.55f,.15f),new Vector3(.7f,1.1f,.7f),wood);
            Shape("SculptureBust",transform,PrimitiveType.Capsule,new Vector3(-5.7f,1.4f,.15f),new Vector3(.45f,.35f,.3f),marble);
            Shape("SculptureHead",transform,PrimitiveType.Sphere,new Vector3(-5.7f,1.95f,.15f),Vector3.one*.45f,marble);
            for (int i=0;i<3;i++) { collectionTextures[i] = new Texture2D(32,32,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point }; textures.Add(collectionTextures[i]); }
            BuildStudio(wood,trim,gold,floor);
        }
        void AddArtwork(int index,Vector3 position,float yaw,float size,Material wood,Material gold,Material iron)
        {
            var frame = Shape(index == 2 ? "PlayerFrame" : "GalleryFrame",transform,PrimitiveType.Cube,position,new Vector3(size+.25f,size+.25f,.18f),wood);
            frame.localRotation = Quaternion.Euler(0,yaw,0);
            Shape("GoldFrame",frame,PrimitiveType.Cube,new Vector3(0,0,-.55f),new Vector3(.97f,.97f,.2f),gold);
            var mat = Material(index == 2 ? "PlayerArtwork" : "OtherArtwork",index == 2 ? Color.white : new Color(.76f,.76f,.76f),true);
            if (index == 2) painting = mat; else mat.mainTexture = OtherArtwork(index);
            galleryArt[index] = mat; galleryOriginal[index] = mat.mainTexture;
            Shape("Painting",frame,PrimitiveType.Quad,new Vector3(0,0,-.7f),new Vector3(size/(size+.25f),size/(size+.25f),1),mat);
            Vector3 front = Quaternion.Euler(0,yaw,0)*Vector3.back;
            artPositions.Add(position); var viewing = position + front*1.65f; viewing.y = 0;
            viewing.x = Mathf.Clamp(viewing.x,-4.4f,4.4f); viewing.z = Mathf.Clamp(viewing.z,-3.7f,3.1f); viewingPositions.Add(viewing);
            Shape("Plaque",frame,PrimitiveType.Cube,new Vector3(.42f,-.65f,0),new Vector3(.18f,.06f,.25f),gold);
            var fixture = new GameObject("ArtworkSpot"+index,typeof(Light)); fixture.transform.SetParent(transform,false);
            fixture.transform.localPosition = position + front*.7f + Vector3.up*1.65f;
            fixture.transform.LookAt(transform.TransformPoint(position));
            var spot = fixture.GetComponent<Light>(); spot.type = LightType.Spot; spot.color = new Color(1,.74f,.44f);
            spot.intensity = index == 2 ? 60 : 35; spot.range = 6; spot.spotAngle = 65; spot.innerSpotAngle = 30; spot.cullingMask = 1 << RoomLayer;
            Shape("LampHousing",fixture.transform,PrimitiveType.Cube,Vector3.zero,new Vector3(.22f,.22f,.3f),iron);
            Shape("LampGlow",fixture.transform,PrimitiveType.Quad,new Vector3(0,0,.16f),new Vector3(.15f,.15f,1),Material("LampLens",new Color(1,.82f,.55f),true));
            Vector3 railCenter = position + front*.65f; railCenter.y = .65f;
            var rail = new GameObject("ArtworkRail").transform; rail.SetParent(transform,false); rail.localPosition = railCenter; rail.localRotation = Quaternion.Euler(0,yaw,0);
            for (int side=-1;side<=1;side+=2)
            {
                Shape("Post",rail,PrimitiveType.Cylinder,new Vector3(side*(size*.5f+.1f),-.2f,0),new Vector3(.07f,.45f,.07f),gold);
                Shape("Finial",rail,PrimitiveType.Sphere,new Vector3(side*(size*.5f+.1f),.26f,0),Vector3.one*.12f,gold);
            }
            Shape("Rope",rail,PrimitiveType.Cube,new Vector3(0,.12f,0),new Vector3(size+.2f,.05f,.05f),iron);
        }
        Texture2D FloorTexture()
        {
            var texture = new Texture2D(64,64,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[4096];
            for (int y=0;y<64;y++) for (int x=0;x<64;x++)
            {
                bool joint = y%32 < 1 || (x+(y/32)*16)%32 < 1;
                int grain = (x*13+y*7)%11;
                pixels[y*64+x] = joint ? new Color32(86,67,52,255) : new Color32((byte)(190+grain),(byte)(156+grain),(byte)(119+grain),255);
            }
            texture.SetPixels32(pixels); texture.Apply(); textures.Add(texture); return texture;
        }
        public void RefreshGalleryArt(SaveData data,Color32[] palette)
        {
            for (int i=0;i<3;i++)
            {
                int slot = collectionSlots[i]; galleryArt[slot].mainTexture = galleryOriginal[slot]; galleryArt[slot].color = new Color(.76f,.76f,.76f);
                var art = data.Collection.Find(a => a.Id == data.GalleryWallIds[i]);
                if (!data.GalleryExpanded || art == null || art.Id == data.HangingId) continue;
                var pixels = new Color32[1024];
                for (int p=0;p<1024;p++) pixels[p] = art.Pixels[p] == 0 ? new Color32(235,229,217,255) : DrawingSurface.ResolveColor(art.Pixels[p],palette,art.CustomColors);
                collectionTextures[i].SetPixels32(pixels); collectionTextures[i].Apply(false);
                galleryArt[slot].mainTexture = collectionTextures[i]; galleryArt[slot].color = Color.white;
            }
        }
        bool expanded;
        void SetGallerySize(bool grow)
        {
            if (expanded == grow) return;
            float ratio = grow ? 1.25f : .8f;
            foreach (Transform child in transform)
            {
                if (child.name == "Studio" || child.name == "GalleryCamera" || child.name == "GalleryLight" || child.name.StartsWith("Visitor")) continue;
                var position = child.localPosition; position.x *= ratio; child.localPosition = position;
                if (child.name == "Floor" || child.name == "BackWall" || (child.name == "Cornice" && child.localScale.x > 1))
                { var size = child.localScale; size.x *= ratio; child.localScale = size; }
            }
            for (int i=0;i<artPositions.Count;i++)
            {
                var art = artPositions[i]; art.x *= ratio; artPositions[i] = art;
                var spot = viewingPositions[i]; spot.x *= ratio; viewingPositions[i] = spot;
            }
            expanded = grow;
        }
        public void Begin(ShowRecord record, Texture artwork,float aspect,bool grow = false)
        {
            SetGallerySize(grow); ShowGalleryCamera(aspect);
            show = record; time = 0; RegularVisitStage = 0; regularIndex = -1; OtherArtworkVisits = 0; EvaluatedCount = Likes = 0; LastEvaluatedIndex = -1; painting.mainTexture = artwork;
            while (visitors.Count < record.Judgment.Visitors.Length) visitors.Add(CreateVisitor(visitors.Count));
            for (int i=0;i<visitors.Count;i++)
            {
                var v = visitors[i]; v.Root.gameObject.SetActive(i < record.Judgment.Visitors.Length);
                if (i >= record.Judgment.Visitors.Length) continue;
                v.Random = new System.Random(Judge.Seed(record.ArtworkId,record.Judgment.Visitors[i].Id,record.Day));
                v.Root.localPosition = new Vector3((i%3-1)*3.3f,0,-3.5f+(i/3)*1.45f);
                v.Destination = Wander(v); v.State = 0; v.Evaluated = false; v.NextVisitAt = 1+(float)v.Random.NextDouble()*7;
                if (record.Judgment.Visitors[i].Id == "warm#0") regularIndex = i;
                v.VisitAt = i == regularIndex ? 6 : 10+i*1.3f+(float)v.Random.NextDouble()*3; v.WalkSpeed = .8f+(float)v.Random.NextDouble()*.3f;
                v.Badge.gameObject.SetActive(false);
            }
            gameObject.SetActive(true);
        }
        public void Advance(float seconds)
        {
            if (show == null) return;
            time += seconds;
            for (int i=0;i<show.Judgment.Visitors.Length;i++)
            {
                var v = visitors[i]; var position = v.Root.localPosition; bool moving = false;
                if (v.State == 0 && time >= v.NextVisitAt)
                {
                    v.ArtIndex = !v.Evaluated && time >= v.VisitAt ? 2 : OtherArt(v);
                    v.State = 1; v.Destination = ViewingSpot(v,i);
                }
                if (!v.Evaluated && v.State == 1 && v.ArtIndex != 2 && time > v.VisitAt+4)
                { v.ArtIndex = 2; v.Destination = ViewingSpot(v,i); }
                Vector3 heading = v.Destination-position; heading.y = 0;
                if (v.State == 2)
                {
                    v.Body.localRotation = Quaternion.LookRotation(new Vector3(artPositions[v.ArtIndex].x-position.x,0,artPositions[v.ArtIndex].z-position.z));
                    if (!v.Reacted && time >= v.ReactAt)
                    {
                        v.Reacted = true;
                        if (v.ArtIndex == 2) { Evaluate(i); if (i == regularIndex) RegularVisitStage = 2; }
                        int reaction = v.ArtIndex == 2 ? (show.Judgment.Visitors[i].Liked ? (int)show.Judgment.Visitors[i].Reaction : 4) : (v.Random.Next(3) == 0 ? 4 : 2+v.Random.Next(2));
                        v.Emotion = reaction; v.BadgeRenderer.sharedMaterial = emoji[reaction];
                        v.StopUntil = time+(reaction == 4 ? 2 : reaction == 3 ? 7 : 5)+(float)v.Random.NextDouble()*.6f;
                    }
                    if (time >= v.StopUntil) { v.Badge.gameObject.SetActive(false); v.State = 0; v.NextVisitAt = time+3+(float)v.Random.NextDouble()*5; v.Destination = Wander(v); }
                }
                else if (heading.sqrMagnitude < .055f)
                {
                    if (v.State == 1)
                    {
                        if (v.ArtIndex != 2) OtherArtworkVisits++;
                        v.BadgeRenderer.sharedMaterial = emoji[1]; v.Badge.gameObject.SetActive(true);
                        v.ReactAt = time+1.5f; v.Reacted = false;
                        v.Body.localRotation = Quaternion.LookRotation(new Vector3(artPositions[v.ArtIndex].x-position.x,0,artPositions[v.ArtIndex].z-position.z));
                        v.State = 2; v.StopUntil = v.ReactAt+8;
                        if (v.ArtIndex == 2 && i == regularIndex && !v.Evaluated) RegularVisitStage = 1;
                    }
                    else v.Destination = Wander(v);
                }
                else
                {
                    Vector3 direction = heading.normalized;
                    for (int j=0;j<show.Judgment.Visitors.Length;j++)
                    {
                        if (i == j) continue;
                        Vector3 away = position-visitors[j].Root.localPosition; float distance = away.sqrMagnitude;
                        if (distance > .001f && distance < .3f) direction += away.normalized*(1-distance/.3f)*.65f;
                    }
                    Vector3 movement = direction.normalized*Mathf.Min(seconds*v.WalkSpeed,heading.magnitude);
                    position += movement; position.x = Mathf.Clamp(position.x,expanded ? -6 : -4.5f,expanded ? 6 : 4.5f); position.z = Mathf.Clamp(position.z,-4,3.5f);
                    // ponytail: 10 visitors use pair checks; use spatial lookup if crowds grow.
                    for (int pass=0;pass<2;pass++) for (int j=0;j<show.Judgment.Visitors.Length;j++)
                    {
                        if (i == j) continue;
                        Vector3 away = position-visitors[j].Root.localPosition; away.y = 0;
                        if (away.sqrMagnitude < .36f)
                            position = visitors[j].Root.localPosition+(away.sqrMagnitude > .0001f ? away.normalized : Vector3.right)*.6f;
                    }
                    position.x = Mathf.Clamp(position.x,expanded ? -6 : -4.5f,expanded ? 6 : 4.5f); position.z = Mathf.Clamp(position.z,-4,3.5f);
                    v.Root.localPosition = position;
                    v.Body.localRotation = Quaternion.Slerp(v.Body.localRotation,Quaternion.LookRotation(direction),Mathf.Min(1,seconds*7));
                    moving = true;
                }
                float step = moving ? Mathf.Sin(time*8+i)*25 : 0;
                v.LeftLeg.localRotation = Quaternion.Euler(step,0,0); v.RightLeg.localRotation = Quaternion.Euler(-step,0,0);
                v.LeftArm.localRotation = Quaternion.Euler(-step*.6f,0,0); v.RightArm.localRotation = Quaternion.Euler(step*.6f,0,0);
                if (v.State == 2)
                {
                    float age = Mathf.Max(0,time-v.ReactAt), pulse = Mathf.Sin(age*5)*Mathf.Clamp01(1-age/2);
                    v.HeadPose.localRotation = !v.Reacted ? Quaternion.Euler(0,0,6*Mathf.Sin(time*2+i)) :
                        v.Emotion == 4 ? Quaternion.Euler(5,14*pulse,0) : Quaternion.Euler((v.Emotion == 3 ? 12 : 8)*pulse,0,0);
                    if (v.Reacted && v.Emotion != 4)
                    {
                        v.RightArm.localRotation = Quaternion.Euler(-(v.Emotion == 3 ? 25 : 12)*Mathf.Abs(pulse),0,0);
                        if (v.Emotion == 3) v.LeftArm.localRotation = Quaternion.Euler(-18*Mathf.Abs(pulse),0,0);
                    }
                }
                else v.HeadPose.localRotation = Quaternion.identity;
                v.Badge.rotation = roomCamera.transform.rotation;
            }
        }
        int OtherArt(Visitor visitor)
        {
            int index = visitor.Random.Next(artPositions.Count-1); return index >= 2 ? index+1 : index;
        }
        Vector3 ViewingSpot(Visitor visitor,int index)
        {
            Vector3 position = viewingPositions[visitor.ArtIndex];
            if (visitor.ArtIndex < 5) { position.x += (index%3-1)*.7f; position.z -= (index/3)*.65f; }
            else { position.z += (index%3-1)*.7f; position.x -= (index/3)*.65f; }
            return position;
        }
        Vector3 Wander(Visitor visitor) => new Vector3((float)visitor.Random.NextDouble()*8.4f-4.2f,0,(float)visitor.Random.NextDouble()*6.8f-3.8f);
        void Evaluate(int index)
        {
            var v = visitors[index]; if (v.Evaluated) return;
            v.Evaluated = true; EvaluatedCount++; if (show.Judgment.Visitors[index].Liked) Likes++;
            LastEvaluatedIndex = index;
        }
        public void Complete() { for (int i=0;i<show.Judgment.Visitors.Length;i++) Evaluate(i); }
        Visitor CreateVisitor(int index)
        {
            var root = new GameObject("Visitor"+index).transform; root.SetParent(transform,false);
            var body = new GameObject("Body").transform; body.SetParent(root,false);
            Shape("Coat",body,PrimitiveType.Capsule,new Vector3(0,.95f,0),new Vector3(.5f,.36f,.35f),clothes[index%clothes.Length]);
            var headPose = new GameObject("HeadPose").transform; headPose.SetParent(body,false); headPose.localPosition = new Vector3(0,1.52f,0);
            Shape("Head",headPose,PrimitiveType.Sphere,Vector3.zero,Vector3.one*.44f,skin);
            Shape("Hair",headPose,PrimitiveType.Sphere,new Vector3(0,.13f,-.035f),new Vector3(.46f,.28f,.44f),shoes);
            if (index == 1) Shape("RegularHat",headPose,PrimitiveType.Cylinder,new Vector3(0,.23f,0),new Vector3(.58f,.055f,.58f),clothes[3]);
            for (int side=-1;side<=1;side+=2) Shape("Eye",headPose,PrimitiveType.Sphere,new Vector3(side*.09f,.03f,.2f),Vector3.one*.035f,shoes);
            var leftLeg = Limb("LeftLeg",body,new Vector3(-.14f,.4f,0),shoes,.19f);
            var rightLeg = Limb("RightLeg",body,new Vector3(.14f,.4f,0),shoes,.19f);
            var leftArm = Limb("LeftArm",body,new Vector3(-.29f,1.2f,0),skin,.22f);
            var rightArm = Limb("RightArm",body,new Vector3(.29f,1.2f,0),skin,.22f);
            var badge = Shape("InterestEmoji",root,PrimitiveType.Quad,new Vector3(0,1.95f,0),Vector3.one*.6f,emoji[0]);
            return new Visitor { Root = root, Body = body, HeadPose = headPose, LeftLeg = leftLeg, RightLeg = rightLeg, LeftArm = leftArm, RightArm = rightArm, Badge = badge, BadgeRenderer = badge.GetComponent<MeshRenderer>() };
        }
        Transform Limb(string name,Transform parent,Vector3 position,Material material,float halfLength)
        {
            var pivot = new GameObject(name).transform; pivot.SetParent(parent,false); pivot.localPosition = position;
            Shape("Limb",pivot,PrimitiveType.Capsule,new Vector3(0,-halfLength,0),new Vector3(.14f,halfLength,.14f),material);
            return pivot;
        }
        Transform Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = RoomLayer;
            go.transform.SetParent(parent,false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = go.GetComponent<Collider>(); if (collider != null) { collider.enabled = false; Destroy(collider); }
            return go.transform;
        }
        Material Material(string name, Color color, bool unlit = false)
        {
            var material = new Material(unlit ? unlitTemplate : litTemplate) { name = name, color = color };
            if (!unlit) material.SetFloat("_Smoothness",.15f);
            materials.Add(material); return material;
        }
        Texture2D OtherArtwork(int index)
        {
            var texture = new Texture2D(32,32,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
            var pixels = new Color32[1024];
            for (int y=0;y<32;y++) for (int x=0;x<32;x++)
            {
                if (index == 0) pixels[y*32+x] = y < 10+x/5 ? new Color32(75,123,102,255) : (x-23)*(x-23)+(y-23)*(y-23)<25 ? new Color32(247,206,100,255) : new Color32(160,199,209,255);
                else if (index == 1) pixels[y*32+x] = (x-16)*(x-16)+(y-16)*(y-16)<75 ? new Color32(187,83,65,255) : new Color32(231,209,168,255);
                else
                {
                    Color32 color;
                    if (index == 3)
                    {
                        color = y < 8 ? new Color32(64,117,149,255) : y < 15+x/5 ? new Color32(87,133,73,255) : new Color32(172,207,219,255);
                        if (x>18 && x<27 && y>=9 && y<17) color = new Color32(240,225,185,255);
                        if (y>=17 && y<22 && System.Math.Abs(x-23)<22-y) color = new Color32(148,68,49,255);
                        if (x==23 && y>=9 && y<13) color = new Color32(66,58,47,255);
                    }
                    else if (index == 4 || index == 6)
                    {
                        color = new Color32(85,112,100,255);
                        bool face = (x-16)*(x-16)+(y-15)*(y-15)<100 || (y>20 && y<29 && (System.Math.Abs(x-9)<(29-y)/2 || System.Math.Abs(x-23)<(29-y)/2));
                        if (face) color = index == 4 ? new Color32(208,154,83,255) : new Color32(221,207,170,255);
                        if ((x==11 || x==21) && y>=15 && y<=17) color = new Color32(28,33,33,255);
                        if (System.Math.Abs(x-16)<2 && y==11) color = new Color32(179,93,88,255);
                    }
                    else if (index == 5)
                    {
                        color = new Color32(59,74,78,255);
                        if (y<10 && x>6 && x<26) color = new Color32(191,145,63,255);
                        if (x>9 && x<23 && y>=9 && y<25) color = new Color32(235,188,129,255);
                        if (y>=22 && y<28 && x>7 && x<25) color = new Color32(94,118,168,255);
                        if ((x==13 || x==20) && y==18) color = new Color32(38,35,35,255);
                        if (x>=15 && x<19 && y==13) color = new Color32(165,77,62,255);
                        if (x>18 && y>26) color = new Color32(227,189,84,255);
                    }
                    else
                    {
                        color = new Color32(143,175,180,255);
                        if (y<7) color = new Color32(104,153,124,255);
                        if (x>=13 && x<=18 && y>=5 && y<12) color = new Color32(172,96,55,255);
                        if ((x==9 || x==16 || x==23) && y>=11 && y<24) color = new Color32(75,116,67,255);
                        for (int flower=0;flower<3;flower++)
                        {
                            int dx=x-(9+flower*7),dy=y-(flower==1 ? 24 : 20);
                            if (dx*dx+dy*dy<26) color = new Color32(241,190,57,255);
                            if (dx*dx+dy*dy<7) color = new Color32(116,72,38,255);
                        }
                    }
                    pixels[y*32+x] = color;
                }
            }
            texture.SetPixels32(pixels); texture.Apply(); textures.Add(texture); return texture;
        }
        Texture2D EmojiTexture(int reaction)
        {
            var texture = new Texture2D(64,64,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[4096]; Color32 ink = new Color32(65,47,28,255);
            for (int y=0;y<64;y++) for (int x=0;x<64;x++)
            {
                int dx=x-32,dy=y-32;
                if (dx*dx+dy*dy <= 29*29) pixels[y*64+x] = dx*dx+dy*dy > 27*27 ? new Color32(224,158,42,255) : new Color32(255,215,78,255);
                bool eye = false;
                for (int side=-1;side<=1;side+=2)
                {
                    int ex=x-(32+side*11),ey=y-39;
                    if (reaction < 2 || reaction == 4) eye |= ex*ex+ey*ey < 10;
                    else if (reaction == 2)
                    {
                        if ((Math.Abs(ex)<=7 && ey>=-6 && ey<=6 && ey>=Math.Abs(ex)-7) || (ex+3)*(ex+3)+(ey-3)*(ey-3)<14 || (ex-3)*(ex-3)+(ey-3)*(ey-3)<14)
                            pixels[y*64+x] = new Color32(216,66,70,255);
                    }
                    else if (reaction == 3)
                    {
                        float angle=Mathf.Atan2(ey,ex),radius=Mathf.Sqrt(ex*ex+ey*ey);
                        if (radius < 5+2.5f*Mathf.Cos(5*angle)) pixels[y*64+x] = new Color32(171,91,14,255);
                    }
                }
                if (eye || (reaction == 0 && Math.Abs(dx)<10 && Math.Abs(y-21)<2) || (reaction == 1 && (x-36)*(x-36)+(y-22)*(y-22)<16) || (reaction == 4 && Math.Abs(dx)<11 && Math.Abs(y-(25-dx*dx/23))<2) || (reaction >= 2 && reaction < 4 && Math.Abs(dx)<13 && Math.Abs(y-(18+dx*dx/23))<2)) pixels[y*64+x] = ink;
            }
            texture.SetPixels32(pixels); texture.Apply(); textures.Add(texture); return texture;
        }
        void OnDestroy()
        {
            if (roomCamera != null) roomCamera.targetTexture = null;
            if (view != null) { view.Release(); Destroy(view); }
            foreach (var material in materials) Destroy(material);
            foreach (var texture in textures) Destroy(texture);
        }
    }
}
