using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Newtonsoft.Json;// Pour le JSON "complexe"

public class Shape
{
    public string name;
    public float[][] points;
    public bool[] isLoop;
    public float length;
}

public class Draw : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Coroutine drawing;
    private bool mouseOver = false;
    private bool isDrawing = false;

    private int totalBasePoints;
    private Vector3[] concat_points; // Points de la forme (coordonnées WorldGUI relatives à l'élément de GUI) : les différents traits sont ici concaténés
    private Vector2[] concat_screenpoints; // Idem (coordonnées absolues; Vector2 suffit ici)

    private float scale;

    private float distanceDrawn = 0;
    private int pointsDrawn = 0;

    private float[] rappr_minsqrdist; // Distances au carré minimales entre le dessin et le point de la forme
    private float rappr_avgsqrdist = float.MaxValue; // Moyenne de minsqrdist

    private float ecart_sumsqrdist = 0;
    private float ecart_avgsqrdist = float.MaxValue;

    // Lien avec les donnéezs
    public Shape shape;
    List<float> timestamps;
    float startingTime;

    private GameObject[] clones;//Necessaires pour créer plusieurs lignes
    
    public ShowValue rapprText;
    public ShowValue ecartText;
    public ShowValue scoreText;
    private GameObject hand;

    public Toggle transparencyMode;

    public Material baseLineMat;
    Image imageComp;
    


    // Start is called before the first frame update
    void Start()
    {
        clones = new GameObject[0];
        hand = GameObject.FindGameObjectsWithTag("Player")[0];
        imageComp = gameObject.GetComponent<Image>();
    }

    public void SetShape(string shapeName)
    {
        EraseAll();
        // On récupère les données du fichier JSON correspondant au shapeName
        var textFile = Resources.Load<TextAsset>("Shapes/" + shapeName);
        if (textFile != null)
        {
            string jsonString = textFile.text;
            this.shape = JsonConvert.DeserializeObject<Shape>(jsonString);

            Color color = baseLineMat.color;
            color.a = 1;
            baseLineMat.color = color;

            DrawBaseShape(this.shape.points,this.shape.isLoop);
            imageComp.color = new Color(0, 0, 0, 0);
            ResetTimer();
        }
        else
        {
            print("Erreur lors de la lecture du fichier : " + "Shapes/" + shapeName + ".json");
        }
        
    }

    void ResetTimer()
    {
        startingTime = Time.time;
        timestamps = new();// Reset les timestamps
    }

    public void EraseDrawnLines()
    {
        foreach (GameObject lineObject in GameObject.FindGameObjectsWithTag("DrawnLine")){
            Destroy(lineObject);
        }

        // Also reset variables
        distanceDrawn = 0;
        pointsDrawn = 0;
        rappr_avgsqrdist = float.MaxValue;
        if (rappr_minsqrdist != null)
        {
            rappr_minsqrdist = new float[rappr_minsqrdist.Length];
            for (int i = 0; i < rappr_minsqrdist.Length; i++)
            {
                rappr_minsqrdist[i] = float.MaxValue;
            }
        }
        ecart_sumsqrdist = 0;
        ecart_avgsqrdist = float.MaxValue;
        ResetTimer();
        SetVisualsFromVariables();
    }

    public void DrawBaseShape(float[][] shapePointsArrays, bool[] isLoop)
    {
        // On supprime les clones précédents
        foreach (GameObject clone in clones) {
            Object.Destroy(clone);
        }

        // Nouveaux clones
        clones = new GameObject[shapePointsArrays.Length];
        totalBasePoints = 0; // Nombre de points de la figure au total
        for (int i = 0; i < shapePointsArrays.Length; i++)
        {
            clones[i] = Instantiate(Resources.Load("baseLine") as GameObject, gameObject.transform.position, Quaternion.identity, gameObject.transform);
            if (isLoop[i]) // Le path doit être dessiné comme une boucle
            {
                clones[i].GetComponent<LineRenderer>().loop = true;
            }
            float[] pathPoints = shapePointsArrays[i];
            totalBasePoints += pathPoints.Length / 2;// Attention à ne pas compter les deux coordonnées (x et y), d'ou le /2
        }

        concat_points = new Vector3[totalBasePoints];
        concat_screenpoints = new Vector2[totalBasePoints];
        rappr_minsqrdist = new float[totalBasePoints];

        Rect rect = GetComponent<RectTransform>().rect; // En WorldGUI
        

        float xmin = rect.min.x;
        float ymin = rect.min.y;
        float xmax = rect.max.x;
        float ymax = rect.max.y;

        scale = Mathf.Min(xmax - xmin, ymax - ymin)/2;

        EraseDrawnLines();

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
                rappr_minsqrdist[nPointsSeen + i] = float.MaxValue;

                concat_points[nPointsSeen + i] = points[i];
                // On doit pourvoir trouver plus simple, mais au moins ça marche (on obtient les points en coordonnées de l'écran en passant par les coordonnées World)
                concat_screenpoints[nPointsSeen + i] = Camera.main.WorldToScreenPoint(baseLine.transform.TransformPoint(points[i]));

            }
            baseLine.positionCount = nPoints;
            baseLine.SetPositions(points);
            nPointsSeen += nPoints;
        }
        
        SetVisualsFromVariables();
    }

    public void EraseAll()
    {
        EraseDrawnLines();
        foreach (GameObject clone in clones)
        {
            Object.Destroy(clone);
        }
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        mouseOver = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        mouseOver = false;
        FinishLine();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0) && mouseOver)
        {
            StartLine();
        }
        if (Input.GetMouseButtonUp(0))
        {
            FinishLine();
        }
    }

    void StartLine()
    {
        if (drawing != null) StopCoroutine(drawing);
        if (concat_points != null)
        {
            timestamps.Add(Time.time - startingTime);
            drawing = StartCoroutine(DrawLine());
            isDrawing = true;
            hand.GetComponent<ParticleSystem>().Play();
        }
    }

    void FinishLine()
    {
        if (isDrawing)
        {
            StopCoroutine(drawing);
            isDrawing = false;
            timestamps.Add(Time.time - startingTime);
        }
        hand.GetComponent<ParticleSystem>().Stop();
    }

    void SetVisualsFromVariables()
    {

        // Calcul du score
        double sum = rappr_avgsqrdist + ecart_avgsqrdist;
        double exigence = 50;
        float alpha = 0.3F;
        double score = System.Math.Pow(1-System.Math.Tanh(exigence*sum),alpha);
        
        scoreText.setValue(score.ToString("P"));
        if (shape != null && transparencyMode.isOn)
        {
            Color linecol = baseLineMat.color;
            linecol.a = Mathf.Lerp(1, 0, distanceDrawn / (2 * shape.length));
            baseLineMat.color = linecol;
        }
        

        // Mise à jour des textes
        if (rapprText != null)
        {
            rapprText.setValue(rappr_avgsqrdist.ToString());
        }
        if (ecartText != null)
        {
            ecartText.setValue(ecart_avgsqrdist.ToString());
        }
    }

    public void ConvertToImage()
    {
        if (shape == null) return;
        Sprite image = Resources.Load<Sprite>("Shapes/"+shape.name);
        if (image == null) return;
        
        imageComp.color = new Color(1,1,1,1);
        imageComp.sprite = image;

        EraseDrawnLines();

        Color color = baseLineMat.color;
        color.a = 0;
        baseLineMat.color = color;
    }

    IEnumerator DrawLine()
    {
        LineRenderer newLine = Instantiate(Resources.Load("Line") as GameObject, new Vector3(0,0,0), Quaternion.identity).GetComponent<LineRenderer>();
        newLine.positionCount = 0;
        Vector2 mousePos;
        Vector2 lastPos = Vector2.zero;// Si non initialisée, engendre une erreur de compilation (mais cette valeur n'est jamais utilisée)

        while (true)
        {
            if (mouseOver){
                mousePos = Input.mousePosition; // En coordonnées de l'écran
                Vector3 CameraMousePos = Camera.main.ScreenToWorldPoint(mousePos); // En coordonnées World (pour la ligne)
                CameraMousePos.z = 0;

                // Ajout d'un nouveau point dessiné
                pointsDrawn++;
                newLine.positionCount++;
                newLine.SetPosition(newLine.positionCount - 1, CameraMousePos);

                hand.transform.position = CameraMousePos;
                
                
                // Au moins le deuxième point de la ligne dessinée
                if (newLine.positionCount > 1)
                {
                    float distToPreviousPoint = (mousePos - lastPos).magnitude / scale;
                    distanceDrawn += distToPreviousPoint;// Distance à l'échelle de la forme

                    // Mise à jour du calcul du rapprochement et d'écartement (on a une pondération par la distance entre deux points, dont on ne peut pas inclure le premier point)
                    float ecart_minsqrdist = float.MaxValue;
                    for (int i = 0; i < totalBasePoints; i++)
                    {
                        float sqrdist = ((mousePos - concat_screenpoints[i]) / scale).sqrMagnitude;
                        if (rappr_minsqrdist[i] > sqrdist) rappr_minsqrdist[i] = sqrdist;
                        if (sqrdist < ecart_minsqrdist) ecart_minsqrdist = sqrdist;
                    }
                    ecart_sumsqrdist += ecart_minsqrdist*distToPreviousPoint;
                    ecart_avgsqrdist = ecart_sumsqrdist / distanceDrawn;

                    float sum = 0;
                    for (int i = 0; i < totalBasePoints; i++)
                    {
                        sum += rappr_minsqrdist[i];
                    }
                    rappr_avgsqrdist = sum / totalBasePoints;
                    SetVisualsFromVariables();
                }
                
                lastPos = mousePos;
            }

            yield return null;
        }
    }
}
