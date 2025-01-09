using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class PlayerScript : MonoBehaviour
{
    public float speed;
    public float maxVelocity;

    private Camera cam;
    private Rigidbody2D rb;
    private GameObject treasure;
    private bool hasTreasure;
    private UIScript uiScript;

    void Start()
    {
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        uiScript = GameObject.Find("UIManager").GetComponent<UIScript>();
        hasTreasure = false;
    }

    void Update()
    {
        // face mouse
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.up = new Vector3(mousePos.x - transform.position.x, mousePos.y - transform.position.y, 0);

        // make camera follow player
        cam.transform.position = new Vector3(transform.position.x, transform.position.y, cam.transform.position.z);

        if (Input.GetKeyDown(KeyCode.Space)) {
            rb.AddForce(new Vector2(mousePos.x - rb.position.x, mousePos.y - rb.position.y).normalized * speed, ForceMode2D.Impulse);

            Vector2 rbVel = rb.velocity;
            rbVel.x = Mathf.Clamp(rbVel.x, -maxVelocity, maxVelocity);
            rbVel.y = Mathf.Clamp(rbVel.y, -maxVelocity, maxVelocity);
            rb.velocity = rbVel;
        }
    }


    private void OnTriggerEnter2D(Collider2D collision) {
        if (collision.gameObject.tag == "TreasurePrompt") {
            treasure = collision.transform.parent.gameObject;
            hasTreasure = true;
            uiScript.ToggleTreasureImage(true);

            Destroy(collision.transform.parent.gameObject);

        }

        if (collision.gameObject.tag == "StartArea" && hasTreasure) {
            uiScript.ToggleDropTreasureText(true);
            uiScript.ToggleTreasureImage(false);
            uiScript.StopTimer();
        }
    }

    public void ResetPlayer() {
        hasTreasure = false;
        treasure = null;
        rb.velocity = Vector2.zero;
    }
}
