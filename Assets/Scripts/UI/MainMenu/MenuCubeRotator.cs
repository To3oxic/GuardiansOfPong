using UnityEngine;
using System.Collections;

public class MenuCubeRotator : MonoBehaviour
{
    [Header("Cube Settings")]
    [SerializeField] private float rotationDuration = 1.6f;
    [SerializeField] private SelectionUI selectionUI;

    private Quaternion targetRotation;
    private bool isRotating = false;

    public enum MenuFace
    {
        MainMenu = 0,      // X = 90
        DeckSelection = 1, // X = 180
        Shop = 2           // X = 270
    }

    private MenuFace currentFace = MenuFace.MainMenu;

    private void Awake()
    {
        targetRotation = transform.rotation;
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        // Right arrow or mouse wheel up - next face
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            RotateToFace((int)currentFace + 1);
        }
        // Left arrow or mouse wheel down - previous face
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            RotateToFace((int)currentFace - 1);
        }

        // Debug keys - jump directly
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            RotateToFace((int)MenuFace.MainMenu);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            RotateToFace((int)MenuFace.DeckSelection);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            RotateToFace((int)MenuFace.Shop);
        }
    }

    private void RotateToFace(int faceIndex)
    {
        if (isRotating)
            return;

        // Wrap around (0, 1, 2, 0, 1, 2...)
        faceIndex = ((faceIndex % 3) + 3) % 3;

        MenuFace targetFace = (MenuFace)faceIndex;

        if (targetFace == currentFace)
            return;

        currentFace = targetFace;

        // Calculate target rotation based on face (X axis rotation)
        switch (targetFace)
        {
            case MenuFace.MainMenu:
                targetRotation = Quaternion.Euler(90, 90, 90);
                if (selectionUI != null) selectionUI.Deactivate();
                break;
            case MenuFace.DeckSelection:
                targetRotation = Quaternion.Euler(0, 90, 90);
                if (selectionUI != null) selectionUI.Activate();
                break;
            case MenuFace.Shop:
                targetRotation = Quaternion.Euler(270, 90, 90);
                if (selectionUI != null) selectionUI.Deactivate();
                break;
        }

        StartCoroutine(SmoothRotate(targetRotation, rotationDuration));
    }

    private IEnumerator SmoothRotate(Quaternion target, float duration)
    {
        isRotating = true;
        float elapsed = 0f;

        Quaternion startRotation = transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Ease in-out cubic for smooth feel


            transform.rotation = Quaternion.Lerp(startRotation, target, t);
            yield return null;
        }

        transform.rotation = target;
        isRotating = false;
    }

    public bool IsRotating() => isRotating;
    public MenuFace GetCurrentFace() => currentFace;
}
