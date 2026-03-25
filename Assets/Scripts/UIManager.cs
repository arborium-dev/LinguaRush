using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ImageClickHandler : MonoBehaviour
{
    [Header("Changable Objects")]
    [SerializeField] private RawImage carImage;
    [SerializeField] private TMP_Text carDesc;

    public void OnImageClicked(string imageName)
    {
        Debug.Log("Clicked: " + imageName);

        switch (imageName)
        {
            case "Belgium":
                SelectionState.SetSelectedCountry("Belgium");
                carDesc.text = "La grande majorité de la population parlant français en Belgique est concentrée dans la région capitale, au centre. C’est là où on trouve la piste de course Spa-Francorchamps, une des pistes les plus célèbres au monde avec une série complexe de détours.";
                SetCarImage("Art/belgium");
                break;

            case "France":
                SelectionState.SetSelectedCountry("France");
                carDesc.text = "La France est le cœur du monde francophone, et mène des événements essentiels pour les sports immobiliers, incluant le 24 Heures du Mans, un symbole de course de distance partout au monde. L’innovation et les traditions de la France influencent le monde francophone et de course automobiliste depuis toujours.";
                SetCarImage("Art/fr");
                break;

            case "Luxembourg":
                SelectionState.SetSelectedCountry("Luxembourg");
                carDesc.text = "Le Luxembourg parle le français comme langue officielle depuis 800 ans, et sa position géographique le rend essentiel pour la culture française et sa pertinence historique. Alexandre Wurz, un coureur fameux autour du monde de la course automobile, est né au Luxembourg, le rendant très important pour son histoire, malgré sa taille.";
                SetCarImage("Art/lux");
                break;
            case "BEGIN":
                if (!SelectionState.TryGetSelectedCountry(out _))
                {
                    Debug.LogWarning("Selecting France as Default; No country was selected.");
                    SelectionState.SetSelectedCountry("France");
                    
                }

                SceneManager.LoadScene("Racing");
                break;
        }
    }

    private void SetCarImage(string resourcePath)
    {
        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"Texture not found at Resources/{resourcePath}");
            return;
        }

        if (carImage == null)
        {
            Debug.LogWarning("carImage is not assigned. Drag your Canvas RawImage component into this field in the Inspector.");
            return;
        }

        carImage.texture = texture;
    }
}
