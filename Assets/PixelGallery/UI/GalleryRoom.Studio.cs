using UnityEngine;

namespace PixelGallery
{
    public sealed partial class GalleryRoom
    {
        public const float FocusSeconds = .8f;
        Transform easelCanvas, painter;
        Transform paintingArm;
        Vector2 brushPoint, brushTarget;
        GameObject studio;
        Material draftMaterial;
        readonly Material[] studioArt = new Material[3];
        readonly Transform[] studioFrames = new Transform[3];
        readonly Texture2D[] studioTextures = new Texture2D[3];
        Vector3 cameraFrom, cameraTo, lookFrom, lookTo;
        float sizeFrom, sizeTo, focusTime = FocusSeconds, paintingTime, brushPhase;
        bool studioMode, focused;
        public bool CanvasReady => studioMode && focused && focusTime >= FocusSeconds;
        public bool IsPainting => paintingTime > 0;
        public Vector3 CameraPosition => roomCamera.transform.localPosition;
        public Vector2 CanvasViewport => roomCamera.WorldToViewportPoint(easelCanvas.position);

        void BuildStudio(Material wood,Material iron,Material gold,Material floor)
        {
            studio = new GameObject("Studio"); studio.transform.SetParent(transform,false);
            studio.transform.localPosition = new Vector3(20,0,0);
            var wall = Material("StudioPlaster",new Color32(75,65,54,255));
            Shape("StudioFloor",studio.transform,PrimitiveType.Cube,new Vector3(0,-.12f,0),new Vector3(8,.24f,8),floor);
            Shape("StudioWall",studio.transform,PrimitiveType.Cube,new Vector3(0,2.3f,2.7f),new Vector3(8,4.6f,.2f),wall);
            Shape("StudioSkirting",studio.transform,PrimitiveType.Cube,new Vector3(0,.15f,2.5f),new Vector3(8,.3f,.15f),wood);
            for (int i=0;i<3;i++)
            {
                var position = new Vector3((i-1)*2.35f,3.55f,2.5f);
                studioFrames[i] = Shape("StudioFrame"+i,studio.transform,PrimitiveType.Cube,position,new Vector3(1.7f,1.7f,.12f),gold);
                studioArt[i] = Material("StudioArtwork"+i,Color.white,true);
                Shape("StudioArtwork"+i,studio.transform,PrimitiveType.Quad,position+new Vector3(0,0,-.07f),new Vector3(1.5f,1.5f,1),studioArt[i]);
                studioTextures[i] = new Texture2D(32,32,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
                textures.Add(studioTextures[i]); studioArt[i].mainTexture = studioTextures[i];
            }
            Shape("EaselLeg",studio.transform,PrimitiveType.Cube,new Vector3(-.75f,1.05f,.15f),new Vector3(.15f,2.1f,.15f),wood).localRotation = Quaternion.Euler(0,0,-12);
            Shape("EaselLeg",studio.transform,PrimitiveType.Cube,new Vector3(.75f,1.05f,.15f),new Vector3(.15f,2.1f,.15f),wood).localRotation = Quaternion.Euler(0,0,12);
            Shape("EaselShelf",studio.transform,PrimitiveType.Cube,new Vector3(0,1,-.18f),new Vector3(2.4f,.16f,.45f),wood);
            Shape("CanvasBacking",studio.transform,PrimitiveType.Cube,new Vector3(0,2,0),new Vector3(2.15f,2.15f,.12f),gold);
            draftMaterial = Material("DraftCanvas",Color.white,true);
            easelCanvas = Shape("EaselCanvas",studio.transform,PrimitiveType.Quad,new Vector3(0,2,-.075f),new Vector3(2,2,1),draftMaterial);
            Shape("SideTable",studio.transform,PrimitiveType.Cube,new Vector3(2.5f,.8f,.5f),new Vector3(1.5f,.15f,1),wood);
            Shape("TableLeg",studio.transform,PrimitiveType.Cube,new Vector3(2.5f,.4f,.5f),new Vector3(.25f,.8f,.25f),iron);
            for (int i=0;i<5;i++) Shape("PaintJar",studio.transform,PrimitiveType.Cylinder,new Vector3(2+i*.23f,.98f,.45f),new Vector3(.16f,.15f,.16f),clothes[i]);
            Shape("StudioPlantPot",studio.transform,PrimitiveType.Cube,new Vector3(-3,.4f,1.8f),new Vector3(.6f,.8f,.6f),wood);
            Shape("StudioPlant",studio.transform,PrimitiveType.Sphere,new Vector3(-3,1.3f,1.8f),new Vector3(.9f,1.6f,.9f),clothes[0]);
            var actor = CreateVisitor(0); painter = actor.Root; painter.name = "Painter"; painter.SetParent(studio.transform,false);
            painter.localPosition = new Vector3(-1.65f,0,.15f); painter.localRotation = Quaternion.Euler(0,80,0);
            actor.Badge.gameObject.SetActive(false); actor.RightArm.gameObject.SetActive(false);
            paintingArm = new GameObject("PaintingArm").transform; paintingArm.SetParent(actor.Body,false);
            paintingArm.localPosition = new Vector3(.29f,1.2f,0);
            Shape("Sleeve",paintingArm,PrimitiveType.Capsule,new Vector3(0,0,.2f),new Vector3(.16f,.2f,.16f),clothes[0]).localRotation = Quaternion.Euler(90,0,0);
            Shape("Hand",paintingArm,PrimitiveType.Sphere,new Vector3(0,0,.43f),Vector3.one*.17f,skin);
            Shape("Brush",paintingArm,PrimitiveType.Cylinder,new Vector3(0,0,.61f),new Vector3(.035f,.16f,.035f),wood).localRotation = Quaternion.Euler(90,0,0);
            Shape("BrushTip",paintingArm,PrimitiveType.Sphere,new Vector3(0,0,.79f),new Vector3(.06f,.06f,.1f),gold);
            var lamp = new GameObject("StudioLight",typeof(Light)); lamp.transform.SetParent(studio.transform,false);
            lamp.transform.localPosition = new Vector3(-1,4,-2); lamp.transform.LookAt(easelCanvas.position);
            var light = lamp.GetComponent<Light>(); light.type = LightType.Spot; light.intensity = 55;
            light.color = new Color(1,.8f,.55f); light.range = 10; light.spotAngle = 100; light.cullingMask = 1 << RoomLayer;
            studio.SetActive(false);
        }
        public Vector2 StudioSlotViewport(int slot) => roomCamera.WorldToViewportPoint(studioFrames[slot].position);
        public void RefreshStudioArt(SaveData data,Color32[] palette)
        {
            for (int slot=0;slot<3;slot++)
            {
                var art = data.Collection.Find(a => a.Id == data.StudioWallIds[slot]);
                var pixels = new Color32[1024];
                for (int i=0;i<1024;i++) pixels[i] = art == null || art.Pixels[i] == 0 ? new Color32(235,229,217,255) : DrawingSurface.ResolveColor(art.Pixels[i],palette,art.CustomColors);
                studioTextures[slot].SetPixels32(pixels); studioTextures[slot].Apply(false);
            }
        }
        public void ShowStudio(Texture draft,float aspect)
        {
            bool returningFromCanvas = studioMode;
            gameObject.SetActive(true); studioMode = true; focused = false; show = null;
            foreach (var visitor in visitors) visitor.Root.gameObject.SetActive(false);
            studio.SetActive(true); draftMaterial.mainTexture = draft; paintingTime = 0;
            roomCamera.aspect = aspect;
            MoveCamera(new Vector3(16,5.4f,-7.8f),new Vector3(20,1.45f,0),4.4f);
            if (!returningFromCanvas)
            {
                focusTime = FocusSeconds; roomCamera.transform.localPosition = cameraTo;
                roomCamera.transform.LookAt(transform.TransformPoint(lookTo)); roomCamera.orthographicSize = sizeTo;
            }
        }
        public void FocusCanvas(Rect viewport,float aspect,bool snap = false)
        {
            focused = true;
            roomCamera.aspect = aspect;
            float size = easelCanvas.localScale.y / (2*viewport.height);
            Vector3 center = transform.InverseTransformPoint(easelCanvas.position);
            var position = center-new Vector3((viewport.center.x-.5f)*size*2*roomCamera.aspect,(viewport.center.y-.5f)*size*2,5);
            MoveCamera(position,position+Vector3.forward*5,size);
            if (snap)
            {
                focusTime = FocusSeconds; roomCamera.transform.localPosition = cameraTo;
                roomCamera.transform.LookAt(transform.TransformPoint(lookTo)); roomCamera.orthographicSize = sizeTo;
            }
        }
        void MoveCamera(Vector3 position,Vector3 look,float size)
        {
            cameraFrom = roomCamera.transform.localPosition; cameraTo = position;
            lookFrom = cameraFrom+roomCamera.transform.forward*8; lookTo = look;
            sizeFrom = roomCamera.orthographicSize; sizeTo = size; focusTime = 0;
        }
        void ShowGalleryCamera(float aspect)
        {
            studioMode = false; studio.SetActive(false); roomCamera.aspect = aspect;
            roomCamera.transform.localPosition = new Vector3(-10,11,-14);
            roomCamera.transform.LookAt(transform.TransformPoint(new Vector3(0,1.4f,0))); roomCamera.orthographicSize = 5.8f;
        }
        public void PaintGesture(Vector2 point)
        {
            if (!CanvasReady) return;
            paintingTime = .5f; brushTarget = point;
        }
        void Update()
        {
            if (!studioMode) return;
            focusTime = Mathf.Min(FocusSeconds,focusTime+Time.deltaTime);
            float t = Mathf.SmoothStep(0,1,focusTime/FocusSeconds);
            roomCamera.transform.localPosition = Vector3.Lerp(cameraFrom,cameraTo,t);
            roomCamera.transform.LookAt(transform.TransformPoint(Vector3.Lerp(lookFrom,lookTo,t)));
            roomCamera.orthographicSize = Mathf.Lerp(sizeFrom,sizeTo,t);
            paintingTime = Mathf.Max(0,paintingTime-Time.deltaTime); brushPhase += Time.deltaTime*12;
            brushPoint = Vector2.Lerp(brushPoint,brushTarget,Mathf.Min(1,Time.deltaTime*10));
            painter.localPosition = new Vector3(-1.65f+(IsPainting ? Mathf.Sin(brushPhase)*.012f : 0),IsPainting ? Mathf.Sin(brushPhase)*.01f : 0,.15f);
            var pose = IsPainting ? Quaternion.Euler(-15-brushPoint.y*25+Mathf.Sin(brushPhase)*8,(brushPoint.x-.5f)*20,0) : Quaternion.Euler(65,0,0);
            paintingArm.localRotation = Quaternion.Slerp(paintingArm.localRotation,pose,Mathf.Min(1,Time.deltaTime*12));
        }
    }
}
