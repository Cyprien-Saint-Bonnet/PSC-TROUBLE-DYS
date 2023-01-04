using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;// Pour le JSON "complexe"

public class DrawTemplate : MonoBehaviour
{
    private GameObject[] clones;//Necessaires pour créer plusieurs lignes

    // Start is called before the first frame update
    void Start()
    {
        clones = new GameObject[0];
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetShape(string shapeName)
    {
        // On récupère les données du fichier JSON correspondant au shapeName
        var textFile = Resources.Load<TextAsset>("Shapes/" + shapeName);
        Shape shape = new();
        if (textFile != null)
        {
            string jsonString = textFile.text;
            shape = JsonConvert.DeserializeObject<Shape>(jsonString);
            DrawBaseShape(shape.points, shape.isLoop);
        }
        else
        {
            print("Erreur lors de la lecture du fichier : " + "Shapes/" + shapeName + ".json");
        }

    }

    public void DrawBaseShape(float[][] shapePointsArrays, bool[] isLoop)
    {
        foreach (GameObject clone in clones)
        {
            Object.Destroy(clone);
        }

        // Nouveaux clones
        clones = new GameObject[shapePointsArrays.Length];
        int totalBasePoints = 0; // Nombre de points de la figure au total
        for (int i = 0; i < shapePointsArrays.Length; i++)
        {
            clones[i] = Instantiate(Resources.Load("templateLine") as GameObject, gameObject.transform.position, Quaternion.identity, gameObject.transform);
            if (isLoop[i]) // Le path doit être dessiné comme une boucle
            {
                clones[i].GetComponent<LineRenderer>().loop = true;
            }
            float[] pathPoints = shapePointsArrays[i];
            totalBasePoints += pathPoints.Length / 2;// Attention à ne pas compter les deux coordonnées (x et y), d'ou le /2
        }

        Rect rect = GetComponent<RectTransform>().rect; // En WorldGUI

        float xmin = rect.min.x;
        float ymin = rect.min.y;
        float xmax = rect.max.x;
        float ymax = rect.max.y;

        float scale = Mathf.Min(xmax - xmin, ymax - ymin) / 2;

        int nPointsSeen = 0;// Nombre de points déjà ajoutés grâce aux paths précédents

        for (int ipath = 0; ipath < shapePointsArrays.Length; ipath++)
        {
            LineRenderer baseLine = clones[ipath].GetComponent<LineRenderer>();
            int nPoints = shapePointsArrays[ipath].Length / 2;

            Vector3[] points = new Vector3[nPoints];
            for (int i = 0; i < nPoints; i++)
            {
                points[i].x = scale * shapePointsArrays[ipath][2 * i]; // On passe de coord Shape en WorldGUI (relatif)
                points[i].y = scale * shapePointsArrays[ipath][2 * i + 1];
            }
            baseLine.positionCount = nPoints;
            baseLine.SetPositions(points);
            nPointsSeen += nPoints;
        }

    }

    public void EraseAll()
    {
        foreach (GameObject clone in clones)
        {
            Object.Destroy(clone);
        }
    }
}
