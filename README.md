# ShipmentHelper

---

> **Important:** An ExileAPI Plugin for Kingsmarch shipments. It provides in-game overlays and suggestions to optimize your resource management.

---

## Features

*   **Shipment Management at a Glance:**
    *   **Shipment Configuration HUD:**
        *   Draggable and remembers its last position.
        *   Displays your currently selected strategy (Recipe, Automatic, or Quota).
        *   Shows required resource goals for your chosen strategy.
        *   Includes a "Prepare Shipment" button to automatically input resources.
        *   For "Automatic" strategies, a dropdown allows you to select the percentage of available resources to send.
          <p></p> <img width="453" height="183" alt="image" src="https://github.com/user-attachments/assets/d027c825-b3e2-46bd-b928-a4ad6a2da5eb" />

    *   **Active Shipment Timer Window:**
        *   Displays remaining time for active Kingsmarch shipments, or "Ready" when completed.
        *   Draggable and remembers its last position.
          <p></p>  <img width="196" height="118" alt="image" src="https://github.com/user-attachments/assets/3a6d6df4-53d1-4472-b9bb-fee512039705" />

<p></p>

*   **Intelligent Port Suggestions (on Map):**
    *   **Automatic Strategies:** The target port for your chosen reward type is highlighted with a green frame. Additionally, relevant ports will display "Shortest (Less Loot)" and "Longest (More Loot)" labels. If the shortest and longest routes are the same, "Best Reward" is shown.
    *   **Recipe & Quota Strategies:** Only the specific target port for your recipe or quota is highlighted with a green frame.
      <p></p> <img width="180" height="153" alt="image" src="https://github.com/user-attachments/assets/3c553f57-97c3-4f50-ae05-4a5857b666ab" />


*   **Thaumaturgic Dust Suggestion:**
    *   Available for "Automatic" strategies.
    *   If "Always Suggest Dust" is enabled and dust is available, it will suggest 1 dust as a fallback.
    *   If your total shipment value is greater than zero, it intelligently suggests the optimal amount of dust (up to your available dust) to maximize value.


---

*   **Strategy Management:**
    *   Easily select between predefined strategies (Automatic, Low-Invest, High-Invest) or your own custom strategies.
    *   Add and manage your own custom shipping strategies directly from the settings.

