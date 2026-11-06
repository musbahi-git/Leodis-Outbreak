using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BootstrapUI : MonoBehaviour
{
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button startButton;

    private void Awake()
    {
        if (statusLabel == null || startButton == null)
        {
            Debug.LogError(
                "BootstrapUI: assign Status Label and Start Button in the Inspector.",
                this
            );

            enabled = false;
            return;
        }

        statusLabel.text = "Ready";
        startButton.onClick.AddListener(HandleStartClicked);
    }

    private void HandleStartClicked()
    {
        statusLabel.text = "Started";
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartClicked);
        }
    }
}