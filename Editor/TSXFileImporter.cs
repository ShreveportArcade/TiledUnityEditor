using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using System.IO;

[ScriptedImporter(1, "tsx", 1)]
public class TSXFileImporter : ScriptedImporter {

    public int pixelsPerUnit = -1;
    public string tsxFilePath;
    public GridLayout.CellSwizzle gridCellSwizzle = GridLayout.CellSwizzle.XYZ;
    public GridLayout.CellLayout gridCellLayout = GridLayout.CellLayout.Rectangle;
    public Vector3 tileAnchor = new Vector3(0.5f, 0.5f, 0);

    static Texture2D _icon;
    static Texture2D icon { 
        get { 
            if (_icon == null) _icon = EditorGUIUtility.IconContent("Tile Icon").image as Texture2D;
            return _icon; 
        } 
    }  

    private static Dictionary<string, Texture2D> tileSetTextures = new Dictionary<string, Texture2D>();
    public static Texture2D GetImageTexture (Tiled.Image image, string texturePath) {
        if (tileSetTextures.ContainsKey(image.source)) return tileSetTextures[image.source];

        texturePath = Path.GetFullPath(texturePath);
        string dataPath = Path.GetFullPath(Application.dataPath);
        texturePath = texturePath.Replace(dataPath, "Assets");
        Texture2D tex = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;

        if (tex != null) tileSetTextures[image.source] = tex;

        return tex;
    }

    public override void OnImportAsset(AssetImportContext ctx) {
        Debug.Log("TileSet: " + ctx.assetPath);
        string name = Path.GetFileNameWithoutExtension(ctx.assetPath);
        Tiled.TileSet tileSet = Tiled.TileSet.Load(ctx.assetPath);
        if (pixelsPerUnit < 0) pixelsPerUnit = tileSet.tileHeight;

        GameObject gameObject = new GameObject(tileSet.name);
        Grid grid = gameObject.AddComponent<Grid>();
        grid.cellLayout = gridCellLayout;
        grid.cellSwizzle = gridCellSwizzle;
        grid.cellSize = new Vector3(tileSet.tileWidth,tileSet.tileHeight,0) / pixelsPerUnit;
        Tilemap tilemap = gameObject.AddComponent<Tilemap>();
        tilemap.tileAnchor = tileAnchor;
        gameObject.AddComponent<TilemapRenderer>();
        ctx.AddObjectToAsset(tileSet.name + ".tsx", gameObject, icon);
        ctx.SetMainObject(gameObject);

        GridPalette gridPalette = ScriptableObject.CreateInstance<GridPalette>();
        gridPalette.cellSizing = GridPalette.CellSizing.Manual;
        gridPalette.name = tileSet.name + " Palette Settings";
        ctx.AddObjectToAsset(tileSet.name + " Palette Settings", gridPalette);
        
        if (tileSet.image != null && !string.IsNullOrEmpty(tileSet.image.source)) {
            string texturePath = Path.Combine(Path.GetDirectoryName(ctx.assetPath), tileSet.image.source);
            Texture2D tex = GetImageTexture(tileSet.image, texturePath);
            if (tileSet.columns == 0 || tileSet.rows == 0) {
                if (tileSet.image.width == 0 || tileSet.image.height == 0) {
                    tileSet.image.width = tex.width;
                    tileSet.image.height = tex.height;
                }
                tileSet.columns = (tileSet.image.width - 2 * tileSet.margin) / (tileSet.tileWidth + tileSet.spacing);
                tileSet.rows = (tileSet.image.width - 2 * tileSet.margin) / (tileSet.tileWidth + tileSet.spacing);
            }

            int columns = tileSet.columns;
            int rows = tileSet.rows;
            Vector2 anchor = Vector2.zero;
            for (int y = 0; y < rows; y++) {
                for (int x = 0; x < columns; x++) {
                    int id = y * columns + x;
                    int gid = id;
                    if (tileSet.firstGIDSpecified) gid += tileSet.firstGID;
                    Tiled.Tile tiledTile = tileSet.GetTile(gid);

                    Tiled.TileRect r = tileSet.GetTileSpriteRect(gid);
                    Rect rect = new Rect(r.x, r.y, r.width, r.height);
                    Sprite sprite = Sprite.Create(tex, rect, anchor, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = tileSet.name + "_" + x + "," + y;
                    bool phys = AddPhysicsToSprite(tiledTile, sprite);
                    ctx.AddObjectToAsset(sprite.name,  sprite);

                    Tile unityTile = ScriptableObject.CreateInstance<Tile>();
                    unityTile.name = tileSet.name + "_" + id;
                    unityTile.sprite = sprite;
                    if (!phys) unityTile.colliderType = Tile.ColliderType.None;
                    ctx.AddObjectToAsset(unityTile.name, unityTile);
                    tilemap.SetTile(new Vector3Int(x,rows-1-y,0), unityTile); 
                }
            }
        }
        else {
            Debug.LogWarning("Importing image tiles not supported yet: " + ctx.assetPath);
        }

        if (!string.IsNullOrEmpty(tsxFilePath)) {
            AssetDatabase.ImportAsset(tsxFilePath);
        }
    }

    static bool AddPhysicsToSprite (Tiled.Tile tile, Sprite sprite) {
        if (tile == null
        || tile.objectGroup == null
        || tile.objectGroup.objects == null
        || tile.objectGroup.objects.Length == 0) return false;

        List<Vector2[]> physicsShape = new List<Vector2[]>();          
        Tiled.TileObject tileObject = tile.objectGroup.objects[0];
        float x = tileObject.x;
        float y = tileObject.y;
        if (tileObject.polygonSpecified) {
            float h = sprite.rect.height;
            Vector2[] path = System.Array.ConvertAll(tileObject.polygon.path, (p) => new Vector2(p.x+x, h-(p.y+y)));
            physicsShape.Add(path);
        }
        else {
            float w = tileObject.width;
            float h = tileObject.height;
            Vector2[] path = new Vector2[] {
                Vector2.zero,
                Vector2.up * h,
                new Vector2(w, h),
                Vector2.right * w
            };
            physicsShape.Add(path);
        }

        sprite.OverridePhysicsShape(physicsShape); 
        return true;
    }
}