﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;

[ScriptedImporter(1, "tmx")]
public class TMXFileImporter : ScriptedImporter {

    public int pixelsPerUnit = -1;

    public static Color TiledColorFromString (string colorStr) {
        Color color = Color.white;
        if (colorStr == null) return color;
        if (colorStr.Length > 8) colorStr = "#" + colorStr.Substring(3) + colorStr.Substring(1, 2);
        ColorUtility.TryParseHtmlString(colorStr, out color); 
        return color;
    }

    public static string TiledColorToString (Color color) {
        if (color == Color.white) return null;
        if (color.a == 1) return "#" + ColorUtility.ToHtmlStringRGB(color).ToLower();
        string colorStr = ColorUtility.ToHtmlStringRGBA(color).ToLower();;
        colorStr = "#" + colorStr.Substring(6, 2) + colorStr.Substring(0, 6);
        return colorStr;
    }

    Tiled.TMXFile tmxFile;
    string tmxFilePath;
    public override void OnImportAsset(AssetImportContext ctx) {
        tmxFilePath = ctx.assetPath;
        tmxFile = Tiled.TMXFile.Load(ctx.assetPath);
        if (pixelsPerUnit < 0) pixelsPerUnit = tmxFile.tileHeight;
        GameObject tileMap = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
        Grid grid = tileMap.AddComponent<Grid>();
        CreateLayers(tmxFile.layers, tileMap.transform);
        ctx.AddObjectToAsset(Path.GetFileName(ctx.assetPath), tileMap);
        ctx.SetMainObject(tileMap);
    }

    void CreateLayers (List<Tiled.Layer> layers, Transform parent) {
        foreach (Tiled.Layer layerData in layers) {
            GameObject layer = new GameObject(layerData.name);
            // SetProperties(layer, layerData.properties);
            layer.transform.SetParent(parent);
            layer.transform.localPosition = new Vector3(layerData.offsetX, -layerData.offsetY, 0) / pixelsPerUnit;

            if (layerData is Tiled.TileLayer) CreateTileLayer(layerData as Tiled.TileLayer, layer);
            else if (layerData is Tiled.ObjectGroup) CreateObjectGroup(layerData as Tiled.ObjectGroup, layer);
            else if (layerData is Tiled.GroupLayer) CreateLayers((layerData as Tiled.GroupLayer).layers, layer.transform);
        }
    }

    Dictionary<int, Tile> _tiles;
    Dictionary<int, Tile> tiles {
        get {
            if (_tiles == null) {
                _tiles = new Dictionary<int, Tile>();
                foreach (Tiled.TileSet tileSet in tmxFile.tileSets) {
                    if (tileSet.hasSource) {
                        string tsxPath = tileSet.source;
                        tsxPath = Path.Combine(Path.GetDirectoryName(tmxFilePath), tsxPath);
                        tsxPath = Path.GetFullPath(tsxPath);

                        string dataPath = Path.GetFullPath(Application.dataPath);
                        tsxPath = tsxPath.Replace(dataPath, "Assets");
                        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(tsxPath);
                        foreach (Object asset in assets) {
                            if (asset is Tile) {
                                Tile tile = asset as Tile;
                                int gid = tileSet.firstGID + int.Parse(tile.name.Split('_')[1]);
                                _tiles[gid] = tile;
                            }
                        }
                    }
                }
            }
            return _tiles;
        }
    }

    public void CreateTileLayer (Tiled.TileLayer layerData, GameObject layer) {
        Tilemap tilemap = layer.AddComponent<Tilemap>();
        tilemap.tileAnchor = Vector3.zero;
        TilemapRenderer renderer = layer.AddComponent<TilemapRenderer>();  
        tilemap.color = TiledColorFromString(layerData.tintColor);
        
        int rows = tmxFile.height;
        int columns = tmxFile.width;
        for (int y = 0; y < rows; y++) {
            for (int x = 0; x < columns; x++) {
                int tileID = layerData.GetTileID(x, y);
                if (tileID == 0) continue;

                Tile tile = tile = tiles[tileID];
                Vector3Int pos = new Vector3Int(x, rows-1-y, 0);
                tilemap.SetTile(pos, tile);
            }
        }
    }

    public void CreateObjectGroup (Tiled.ObjectGroup groupData, GameObject group) {
        float w = tmxFile.tileWidth / pixelsPerUnit;
        float h = tmxFile.tileHeight / pixelsPerUnit;
        foreach (Tiled.TileObject tileObject in groupData.objects) {
            bool isPrefab = tileObject.properties != null && System.Array.Exists(tileObject.properties, (p) => p.name == "prefab");
            if (isPrefab) CreatePrefabTile(tileObject, group);
            else CreateSpriteTile(tileObject, group);
        }
    }

    public void CreatePrefabTile (Tiled.TileObject tileObject, GameObject group) {
        int tileID = (int)tileObject.gid;
        Tiled.Property prefabProp = System.Array.Find(tileObject.properties, (p) => p.name == "prefab");

        string prefabPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(tmxFilePath), prefabProp.val));
        string dataPath = Path.GetFullPath(Application.dataPath);
        prefabPath = prefabPath.Replace(dataPath, "Assets");
        GameObject prefab = AssetDatabase.LoadAssetAtPath(prefabPath, typeof(GameObject)) as GameObject;
        GameObject g = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        g.transform.SetParent(group.transform);
        float y = tmxFile.height * tmxFile.tileHeight - tileObject.y;
        g.transform.localPosition = new Vector3(tileObject.x, y, 0) / pixelsPerUnit;
        g.transform.localEulerAngles = Vector3.forward * -tileObject.rotation;
        SetProperties(g, tileObject.properties);
        g.SetActive(tileObject.visible);
    }

    public void SetProperties (GameObject g, Tiled.Property[] props) {
        if (props == null) return;
        foreach (Tiled.Property prop in props) {
            if (!prop.name.Contains(".")) continue;
            string[] classVar = prop.name.Split('.');
            Object o = g as Object;
            System.Type type = System.Type.GetType(classVar[0]);
            if (classVar[0] == "GameObject") {
                if (classVar[1] == "layer" && !prop.typeSpecified) {
                    g.layer = LayerMask.NameToLayer(prop.val);
                    continue;
                }
            }
            else {
                if (type == null) type = System.Type.GetType("UnityEngine.Rendering."+classVar[0]+",UnityEngine");
                if (type == null) continue;
                Component c = g.GetComponent(type);
                if (c == null) c = g.AddComponent(type);
                o = c as Object;
            }
            FieldInfo fieldInfo = type.GetField(classVar[1], BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo != null) {
                switch (prop.type) {
                    case "float":
                        fieldInfo.SetValue(o, float.Parse(prop.val));
                        break;
                    case "int":
                        fieldInfo.SetValue(o, int.Parse(prop.val));
                        break;
                    case "bool":
                        fieldInfo.SetValue(o, bool.Parse(prop.val));
                        break;
                    case "color":
                        fieldInfo.SetValue(o, TiledColorFromString(prop.val));
                        break;
                    default:
                        fieldInfo.SetValue(o, prop.val);
                        break;
                }
                continue;
            }
            PropertyInfo propInfo = type.GetProperty(classVar[1], BindingFlags.Public | BindingFlags.Instance);
            if (propInfo != null) {
                switch (prop.type) {
                    case "float":
                        propInfo.SetValue(o, float.Parse(prop.val));
                        break;
                    case "int":
                        propInfo.SetValue(o, int.Parse(prop.val));
                        break;
                    case "bool":
                        propInfo.SetValue(o, bool.Parse(prop.val));
                        break;
                    case "color":
                        propInfo.SetValue(o, TiledColorFromString(prop.val));
                        break;
                    default:
                        propInfo.SetValue(o, prop.val);
                        break;
                }
                continue;
            }
        }
    }

    public void CreateSpriteTile (Tiled.TileObject tileObject, GameObject group) {
        int tileID = (int)tileObject.gid;
        GameObject g = new GameObject(tileObject.name);
        g.transform.SetParent(group.transform);
        float y = tmxFile.height * tmxFile.tileHeight - tileObject.y;
        g.transform.localPosition = new Vector3(tileObject.x, y, 0) / pixelsPerUnit;
        g.transform.localEulerAngles = Vector3.forward * -tileObject.rotation;

        SpriteRenderer sprite = g.AddComponent<SpriteRenderer>();
        sprite.flipX = Tiled.TMXFile.FlippedHorizontally(tileObject.gid);
        sprite.flipY = Tiled.TMXFile.FlippedVertically(tileObject.gid);
        Tiled.TileSet tileSet = tmxFile.GetTileSetByTileID(tileID);
        // if (tileSet != null && tileSet.image != null && !string.IsNullOrEmpty(tileSet.image.source)) {
        //     g.transform.localScale = new Vector3(tileObject.width, tileObject.height, pixelsPerUnit) / pixelsPerUnit;
        //     int tileSetIndex = System.Array.IndexOf(tmxFile.tileSets, tileSet);
        //     Material mat = tileSetMaterials[tileSetIndex];
        //     Texture2D tex = mat.mainTexture as Texture2D;
        //     TileRect r = tileSet.GetTileSpriteRect(tileID);
        //     Rect rect = new Rect(r.x, r.y, r.width, r.height);
        //     Vector2 pivot = Vector2.zero;
        //     sprite.sprite = Sprite.Create(tex, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        // }
        // else {
        //     int tileSetIndex = System.Array.IndexOf(tmxFile.tileSets, tileSet);
        //     sprite.sprite = tileSetSprites[tileSetIndex][tileID-tileSet.firstGID];
        // }

        SetProperties(g, tileObject.properties);
    }

}