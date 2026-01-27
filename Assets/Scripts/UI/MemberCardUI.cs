using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// UI component for displaying a group member card
/// </summary>
public class MemberCardUI : MonoBehaviour
{
    [Header("Display Elements")]
    [SerializeField] private TextMeshProUGUI userNameText;
    [SerializeField] private TextMeshProUGUI permissionText;
    [SerializeField] private TextMeshProUGUI characterCountText;
    [SerializeField] private TextMeshProUGUI joinedDateText;

    [Header("Action Buttons")]
    [SerializeField] private Button changePermissionButton;
    [SerializeField] private Button removeMemberButton;

    // Events
    public Action<GroupMember> OnChangePermissionRequested;
    public Action<GroupMember> OnRemoveMemberRequested;

    private GroupMember currentMember;

    public void SetupCard(GroupMember member, string userName, Color permissionColor)
    {
        currentMember = member;

        // Set user name
        if (userNameText != null)
            userNameText.text = userName;

        // Set permission with color
        if (permissionText != null)
        {
            permissionText.text = member.permission.ToString();
            permissionText.color = permissionColor;
        }

        // Set character count
        if (characterCountText != null)
            characterCountText.text = $"{member.characterIds?.Count ?? 0} characters";

        // Set joined date
        if (joinedDateText != null)
        {
            TimeSpan timeSince = DateTime.Now - member.joinedDate;
            string timeText = timeSince.TotalDays < 1
                ? "Joined today"
                : $"Joined {(int)timeSince.TotalDays} days ago";
            joinedDateText.text = timeText;
        }

        SetupButtons();
    }

    private void SetupButtons()
    {
        if (changePermissionButton != null)
        {
            changePermissionButton.onClick.RemoveAllListeners();
            changePermissionButton.onClick.AddListener(() => OnChangePermissionRequested?.Invoke(currentMember));
        }

        if (removeMemberButton != null)
        {
            removeMemberButton.onClick.RemoveAllListeners();
            removeMemberButton.onClick.AddListener(() => OnRemoveMemberRequested?.Invoke(currentMember));
        }
    }

    public void SetButtonsInteractable(bool canChangePermission, bool canRemove)
    {
        if (changePermissionButton != null)
            changePermissionButton.interactable = canChangePermission;

        if (removeMemberButton != null)
            removeMemberButton.interactable = canRemove;
    }
}