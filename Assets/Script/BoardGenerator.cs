using UnityEngine;

public class BoardGenerator : MonoBehaviour
{
  [Header("マテリアル設定")]
  public Material whiteMat;
  public Material blackMat;

  void Start()
  {
    GenerateBoard();
    GenerateBench();
  }

  void GenerateBoard()
  {
    // Z位置を +1.0 シフトさせて画面上のショップUIとの重なりを防止（Z: -2.5 ～ +4.5）
    for (int x = 0; x < 8; x++)
    {
      for (int z = 0; z < 8; z++)
      {
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tile.name = $"Tile_{x}_{z}";
        tile.transform.SetParent(transform);

        tile.transform.position = new Vector3(x - 3.5f, 0.001f, z - 2.5f); // Z+1.0シフト
        tile.transform.rotation = Quaternion.Euler(90, 0, 0);

        bool isWhite = (x + z) % 2 == 0;
        Renderer ren = tile.GetComponent<Renderer>();
        if (ren != null)
        {
          ren.material = isWhite ? whiteMat : blackMat;
        }
      }
    }
  }

  void GenerateBench()
  {
    // ベンチ（X = 5.5）も同様にZ位置を+1.0シフト（Z: -2.5 ～ +4.5）
    for (int z = 0; z < 8; z++)
    {
      GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
      tile.name = $"BenchTile_{z}";
      tile.transform.SetParent(transform);

      tile.transform.position = new Vector3(5.5f, 0.001f, z - 2.5f); // Z+1.0シフト
      tile.transform.rotation = Quaternion.Euler(90, 0, 0);

      Renderer ren = tile.GetComponent<Renderer>();
      if (ren != null)
      {
        ren.material.color = new Color(0.3f, 0.3f, 0.3f);
      }
    }
  }
}