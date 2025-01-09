using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIScript : MonoBehaviour
{
    public Image treasureImage;
    public TextMeshProUGUI dropTreasureText;
    public TextMeshProUGUI timerText;

    private Stopwatch watch;

    void Start()
    {
        watch = new Stopwatch();
    }

    private void Update() {
        timerText.text = watch.Elapsed.TotalSeconds.ToString("0") + "s";
    }

    public void ToggleTreasureImage(bool a) {
        treasureImage.gameObject.SetActive(a);
    }

    public void ToggleDropTreasureText(bool a) {
        dropTreasureText.gameObject.SetActive(a);
    }

    public void StartTimer() {
        watch.Reset();
        watch.Start();

        dropTreasureText.text = "You've retrieved the treasure!";
    }

    public void StopTimer() {
        watch.Stop();

        dropTreasureText.text += "\n Time: " + watch.Elapsed.TotalSeconds.ToString("0") + "s";
    }
}
