using UnityEngine;

[System.Serializable]
public enum ViewMode
{
    Grid,       // Cards in a grid layout
    List        // Items in a vertical list
}

[System.Serializable]
public class UISettings
{
    [Header("View Settings")]
    public ViewMode currentViewMode = ViewMode.Grid;
    public bool showItemImages = true;
    public bool showPlayerNotes = true;

    [Header("Grid Settings")]
    [Range(200, 400)]
    public float gridCardWidth = 300f;
    [Range(150, 300)]
    public float gridCardHeight = 200f;

    [Header("List Settings")]
    [Range(400, 800)]
    public float listItemWidth = 600f;
    [Range(60, 120)]
    public float listItemHeight = 80f;
}