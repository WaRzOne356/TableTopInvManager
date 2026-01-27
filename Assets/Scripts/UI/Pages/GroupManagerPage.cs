using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InventorySystem.UI.Navigation;
using InventorySystem.UI.Pages;
using InventorySystem.Data;

namespace InventorySystem.UI.Pages
{
    /// <summary>
    /// Page for managing groups - create, edit, delete, manage members
    /// </summary>
    public class GroupManagementPage : UIPage
    {
        [Header("Current Group Display")]
        [SerializeField] private Image groupAvatarImage;
        [SerializeField] private TextMeshProUGUI groupNameText;
        [SerializeField] private TextMeshProUGUI groupDescriptionText;
        [SerializeField] private TextMeshProUGUI memberCountText;
        [SerializeField] private TextMeshProUGUI createdDateText;
        [SerializeField] private Button editGroupButton;
        [SerializeField] private Button deleteGroupButton;
        [SerializeField] private Button switchGroupButton;

        [Header("Members Section")]
        [SerializeField] private Transform membersContainer;
        [SerializeField] private GameObject memberCardPrefab;
        [SerializeField] private Button addMemberButton;
        [SerializeField] private TextMeshProUGUI noMembersText;

        [Header("Characters Section")]
        [SerializeField] private Transform charactersContainer;
        [SerializeField] private GameObject characterEntryPrefab;
        [SerializeField] private Button addCharacterButton;
        [SerializeField] private TextMeshProUGUI noCharactersText;

        [Header("Group List Section")]
        [SerializeField] private Transform groupListContainer;
        [SerializeField] private GameObject groupListItemPrefab;
        [SerializeField] private Button createGroupButton;
        [SerializeField] private GameObject groupListPanel;  // Panel that shows all groups

        [Header("Dialogs")]
        [SerializeField] private CreateGroupDialog createGroupDialog;
        [SerializeField] private EditGroupDialog editGroupDialog;
        [SerializeField] private AddMemberDialog addMemberDialog;
        [SerializeField] private AddCharacterToGroupDialog addCharacterDialog;
        [SerializeField] private ConfirmDeleteDialog confirmDeleteDialog;
        [SerializeField] private CreateUserDialog createUserDialog;

        [Header("Admin Controls")]  
        [SerializeField] private Button createUserButton;

        // State
        private Group currentGroup;
        private List<GameObject> spawnedMemberCards = new List<GameObject>();
        private List<GameObject> spawnedCharacterEntries = new List<GameObject>();
        private List<GameObject> spawnedGroupListItems = new List<GameObject>();

        void Awake()
        {
            pageType = UIPageType.GroupManagement;
            pageTitle = "Group Management";

            SetupEventHandlers();
        }

        private void Start()
        {
            // Subscribe to events with delay
            StartCoroutine(InitializePageDelayed());
        }

        private System.Collections.IEnumerator InitializePageDelayed()
        {
            // Wait for managers to fully initialize
            yield return new WaitForSeconds(0.2f);

            var groupManager = GroupManager.Instance;
            if (groupManager != null)
            {
                Debug.Log("[GroupManagement] Subscribing to GroupManager events");
                groupManager.OnGroupsChanged += OnGroupsChanged;
                groupManager.OnCurrentGroupChanged += OnCurrentGroupChanged;
            }
            else
            {
                Debug.LogError("[GroupManagement] GroupManager.Instance is NULL!");
            }

            var userManager = UserManager.Instance;
            Debug.Log($"[GroupManagement] UserManager.Instance: {userManager != null}");

            if (userManager != null)
            {
                Debug.Log($"[GroupManagement] UserManager has OnUsersChanged: {userManager.GetType().GetField("OnUsersChanged") != null}");

              //  userManager.OnUsersChanged += OnUsersChanged;
            }
            else
            {
                Debug.LogError("[GroupManagement] UserManager.Instance is NULL!");
            }

            // Initial refresh
            RefreshContent();
        }

        private void SetupEventHandlers()
        {
            Debug.Log("[GroupManagement] Setting up event handlers...");
            Debug.Log($"  editGroupButton: {editGroupButton != null}");
            Debug.Log($"  deleteGroupButton: {deleteGroupButton != null}");
            Debug.Log($"  switchGroupButton: {switchGroupButton != null}");
            Debug.Log($"  addMemberButton: {addMemberButton != null}");
            Debug.Log($"  addCharacterButton: {addCharacterButton != null}");

            // Current Group Actions
            editGroupButton?.onClick.AddListener(OpenEditGroupDialog);
            deleteGroupButton?.onClick.AddListener(ConfirmDeleteGroup);
            switchGroupButton?.onClick.AddListener(ToggleGroupListPanel);

            // Members Section
            addMemberButton?.onClick.AddListener(OpenAddMemberDialog);

            // Characters Section
            addCharacterButton?.onClick.AddListener(OpenAddCharacterDialog);

            // Admin Controls - ADD THIS
            createUserButton?.onClick.AddListener(OpenCreateUserDialog);
        }

        protected override void RefreshContent()
        {
            if (GroupManager.Instance == null || UserManager.Instance == null)
            {
                Debug.LogWarning("[GroupManagement] Managers not ready, skipping refresh");
                return;
            }
            LoadCurrentGroup();
            RefreshMembersDisplay();
            RefreshCharactersDisplay();
        }

        // =====================================================================
        // GROUP DISPLAY
        // =====================================================================

        private void LoadCurrentGroup()
        {
            var groupManager = GroupManager.Instance;
            if (groupManager == null)
            {
                Debug.LogWarning("[GroupManagement] GroupManager not available");
                return;
            }

            currentGroup = groupManager.GetCurrentGroup();

            if (currentGroup == null)
            {
                Debug.LogWarning("[GroupManagement] No current group found");
                return;
            }
            // Update UI
            if (groupNameText != null)
                groupNameText.text = currentGroup.groupName;

            if (groupDescriptionText != null)
            {
                groupDescriptionText.text = string.IsNullOrEmpty(currentGroup.description)
                    ? "No description"
                    : currentGroup.description;
            }

            if (memberCountText != null)
                memberCountText.text = $"{currentGroup.members.Count} Members";

            if (createdDateText != null)
                createdDateText.text = $"Created: {currentGroup.dateCreated:MMM dd, yyyy}";

            // Load avatar
            LoadGroupAvatar();

            // Check permissions for edit/delete
            UpdatePermissionButtons();
        }

        private void LoadGroupAvatar()
        {
            if (groupAvatarImage == null || currentGroup == null) return;

            // TODO: Load avatar from Resources or custom path
            // For now, use a default sprite
            // groupAvatarImage.sprite = Resources.Load<Sprite>($"GroupAvatars/{currentGroup.avatarSpriteName}");
        }

        private void UpdatePermissionButtons()
        {
            if (currentGroup == null) return;

            string currentUserId = PlayerPrefs.GetString("UserId", "");
            bool isCreator = currentGroup.creatorUserId == currentUserId;
            bool isAdmin = currentGroup.UserHasPermission(currentUserId, GroupPermission.Admin);

            // Only creator or admin can edit
            if (editGroupButton != null)
                editGroupButton.interactable = isCreator || isAdmin;

            // Only creator can delete
            if (deleteGroupButton != null)
                deleteGroupButton.interactable = isCreator;

            // Admin or moderator can add members
            if (addMemberButton != null)
                addMemberButton.interactable = currentGroup.UserHasPermission(currentUserId, GroupPermission.Moderator);
            
            //Only admin can create users
            if (createUserButton != null)
                createUserButton.gameObject.SetActive(isAdmin || isCreator);
        }

        // =====================================================================
        // MEMBERS DISPLAY
        // =====================================================================

        private void RefreshMembersDisplay()
        {
            ClearMemberCards();

            if (currentGroup == null || currentGroup.members.Count == 0)
            {
                if (noMembersText != null)
                {
                    noMembersText.gameObject.SetActive(true);
                    noMembersText.text = "No members in this group";
                }
                return;
            }

            if (noMembersText != null)
                noMembersText.gameObject.SetActive(false);

            // Create member cards
            foreach (var member in currentGroup.members.OrderByDescending(m => m.permission))
            {
                CreateMemberCard(member);
            }
        }

        private void CreateMemberCard(GroupMember member)
        {
            if (memberCardPrefab == null || membersContainer == null)
            {
                Debug.LogError("[GroupManagement] memberCardPrefab or membersContainer is null!");
                return;
            }

            if (member == null)
            {
                Debug.LogError("[GroupManagement] member is null!");
                return;
            }
            Debug.Log($"[GroupManagement] === CreateMemberCard Db ug=====");
            Debug.Log($"[GroupManagement]  member.userid is: '{member.userId}'");
            Debug.Log($"[GroupManagement]  member.userid type { member.userId.GetType()} ");

            var allUserss = UserManager.Instance?.GetUsers();
            if(allUserss!= null)
            {
                foreach ( var u in allUserss)
                {
                    Debug.Log($"[GroupManagement] Comparing to userclientid: '{u.clientId}' as string ' {u.clientId.ToString()}");
                    Debug.Log($"[GroupManagement]     match? {u.clientId.ToString() == member.userId}");
                }
            }

            Debug.Log($"[GroupManagement] Creating card for member with userId: {member.userId}");

            var cardObj = Instantiate(memberCardPrefab, membersContainer);
            spawnedMemberCards.Add(cardObj);

            // Get user info
            var userManager = UserManager.Instance;
            string userName = "Unknown User";

            if (userManager != null)
            {
                var allUsers = userManager.GetUsers();
                Debug.Log($"[GroupManagement] UserManager has {allUsers.Count} users");

                // Log all users to see what we have
                foreach (var u in allUsers)
                {
                    Debug.Log($"  User: {u.userName} (clientId: {u.clientId})");
                }

                var userInfo = allUsers.FirstOrDefault(u => u.userId == member.userId);

                // IMPORTANT: Check userName field, not userInfo itself (structs can't be null)
                if (!string.IsNullOrEmpty(userInfo.userId))
                {
                    userName = userInfo.userName.ToString();
                    Debug.Log($"[GroupManagement] Found user: {userName}");
                }
                else
                {
                    Debug.LogWarning($"[GroupManagement] User not found for member with userId: {member.userId}");

                    // Try to find by partial match
                    var partialMatch = allUsers.FirstOrDefault(u =>
                        member.userId.Contains(u.userId) ||
                        u.userId.Contains(member.userId)
                    );

                    if (!string.IsNullOrEmpty(partialMatch.userName))
                    {
                        userName = partialMatch.userName.ToString() + " (approx match)";
                        Debug.LogWarning($"[GroupManagement] Found approximate match: {userName}");
                    }
                    else
                    {
                        userName = $"Unknown ({member.userId.Substring(0, Math.Min(8, member.userId.Length))})";
                    }
                }
            }
            else
            {
                Debug.LogError("[GroupManagement] UserManager is null!");
            }

            // Get permission color
            Color permissionColor = GetPermissionColor(member.permission);

            // Use the MemberCardUI component
            var cardUI = cardObj.GetComponent<MemberCardUI>();
            if (cardUI != null)
            {
                cardUI.SetupCard(member, userName, permissionColor);

                // Subscribe to events
                cardUI.OnChangePermissionRequested += OpenChangePermissionDialog;
                cardUI.OnRemoveMemberRequested += ConfirmRemoveMember;

                // Set button interactivity based on permissions
                if (currentGroup != null)
                {
                    string currentUserId = PlayerPrefs.GetString("UserId", "");
                    bool canModify = currentGroup.UserHasPermission(currentUserId, GroupPermission.Admin);
                    bool isCreator = currentGroup.creatorUserId == member.userId;

                    cardUI.SetButtonsInteractable(canModify, canModify && !isCreator);
                }
            }
            else
            {
                Debug.LogError("[GroupManagement] MemberCard prefab missing MemberCardUI component!");
            }
        }
        private Color GetPermissionColor(GroupPermission permission)
        {
            return permission switch
            {
                GroupPermission.Admin => new Color(1f, 0.3f, 0.3f),      // Red
                GroupPermission.Moderator => new Color(1f, 0.6f, 0.2f),  // Orange
                GroupPermission.Editor => new Color(0.4f, 0.8f, 1f),     // Blue
                GroupPermission.Member => new Color(0.4f, 1f, 0.4f),     // Green
                GroupPermission.Viewer => new Color(0.7f, 0.7f, 0.7f),   // Gray
                _ => Color.white
            };
        }

        private void ClearMemberCards()
        {
            foreach (var card in spawnedMemberCards)
            {
                if (card != null)
                    Destroy(card);
            }
            spawnedMemberCards.Clear();
        }

        // =====================================================================
        // CHARACTERS DISPLAY
        // =====================================================================

        private void RefreshCharactersDisplay()
        {
            ClearCharacterEntries();

            var groupManager = GroupManager.Instance;
            if (groupManager == null || currentGroup == null) return;

            var groupCharacters = groupManager.GetGroupCharacters(currentGroup.groupId);

            if (groupCharacters.Count == 0)
            {
                if (noCharactersText != null)
                {
                    noCharactersText.gameObject.SetActive(true);
                    noCharactersText.text = "No characters in this group";
                }
                return;
            }

            if (noCharactersText != null)
                noCharactersText.gameObject.SetActive(false);

            // Create character entries
            foreach (var character in groupCharacters.OrderBy(c => c.characterName))
            {
                CreateCharacterEntry(character);
            }
        }

        private void CreateCharacterEntry(PlayerCharacter character)
        {
            if (characterEntryPrefab == null || charactersContainer == null) return;

            var entryObj = Instantiate(characterEntryPrefab, charactersContainer);
            spawnedCharacterEntries.Add(entryObj);

            // Populate entry
            var nameText = entryObj.transform.Find("CharacterNameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
                nameText.text = character.characterName;

            var classLevelText = entryObj.transform.Find("ClassLevelText")?.GetComponent<TextMeshProUGUI>();
            if (classLevelText != null)
                classLevelText.text = $"Level {character.level} {character.characterClass}";

            // Find owner name
            var userManager = UserManager.Instance;
            string ownerName = "Unknown";
            if (userManager != null)
            {
                var owner = userManager.GetUsers().FirstOrDefault(u => u.userId == character.ownerUserId);
                if (owner.userName != null)
                    ownerName = owner.userName.ToString();
            }
            else
            {
                Debug.Log("[groupmanager-createcharacterentry] USERMANAGER IS NULL");
            }

            var ownerText = entryObj.transform.Find("OwnerText")?.GetComponent<TextMeshProUGUI>();
            if (ownerText != null)
                ownerText.text = $"Owner: {ownerName}";

            // Active indicator
            var activeIndicator = entryObj.transform.Find("ActiveIndicator")?.gameObject;
            if (activeIndicator != null)
                activeIndicator.SetActive(character.isActive);

            // Remove button
            var removeButton = entryObj.transform.Find("RemoveButton")?.GetComponent<Button>();
            if (removeButton != null)
            {
                removeButton.onClick.AddListener(() => ConfirmRemoveCharacter(character));

                // Only admins or character owner can remove
                string currentUserId = PlayerPrefs.GetString("UserId", "");
                bool isOwner = character.ownerUserId == currentUserId;
                bool isAdmin = currentGroup.UserHasPermission(currentUserId, GroupPermission.Admin);
                removeButton.interactable = isOwner || isAdmin;
            }
        }

        private void ClearCharacterEntries()
        {
            foreach (var entry in spawnedCharacterEntries)
            {
                if (entry != null)
                    Destroy(entry);
            }
            spawnedCharacterEntries.Clear();
        }

        // =====================================================================
        // GROUP LIST PANEL
        // =====================================================================

        private void ToggleGroupListPanel()
        {
            if (groupListPanel == null) return;

            bool isActive = groupListPanel.activeSelf;
            groupListPanel.SetActive(!isActive);

            if (!isActive)
            {
                RefreshGroupList();
            }
        }

        private void RefreshGroupList()
        {
            ClearGroupListItems();

            var groupManager = GroupManager.Instance;
            if (groupManager == null)
            {
                Debug.LogWarning("[GroupManagement] GroupManager not available");
                return;
            }

            string currentUserId = PlayerPrefs.GetString("UserId", "");

            // Get current user to check permission
            var userManager = UserManager.Instance;
            bool isAdminOrModerator = false;

            if (userManager != null)
            {
                var allUsers = userManager.GetUsers();
                var currentUser = allUsers.FirstOrDefault(u => u.userId == currentUserId);

                // Check if we found the user AND they have a valid permission
                if (currentUser.userName != null)  // userName being null means FirstOrDefault returned default struct
                {
                    isAdminOrModerator = currentUser.permission >= GroupPermission.Moderator;
                    Debug.Log($"[GroupManagement] User permission: {currentUser.permission}, isAdminOrModerator: {isAdminOrModerator}");
                }
                else
                {
                    Debug.LogWarning($"[GroupManagement] Current user not found in UserManager. UserId: {currentUserId}");
                }
            }

            // Show all groups if admin/moderator, otherwise only user's groups
            List<Group> groupsToShow;

            if (isAdminOrModerator)
            {
                groupsToShow = groupManager.GetAllGroups().ToList();
                Debug.Log($"[GroupManagement] Admin/Moderator - showing all {groupsToShow.Count} groups");
            }
            else
            {
                groupsToShow = groupManager.GetUserGroups(currentUserId);
                Debug.Log($"[GroupManagement] Regular user - showing {groupsToShow.Count} groups");
            }

            if (groupsToShow == null || groupsToShow.Count == 0)
            {
                Debug.Log("[GroupManagement] No groups to show");
                return;
            }

            foreach (var group in groupsToShow.OrderByDescending(g => g.lastActivity))
            {
                CreateGroupListItem(group);
            }
        }

        private void CreateGroupListItem(Group group)
        {
            if (groupListItemPrefab == null || groupListContainer == null) return;

            Debug.Log($"[GroupManagement] Creating list item for group: {group.groupName}");

            var itemObj = Instantiate(groupListItemPrefab, groupListContainer);
            spawnedGroupListItems.Add(itemObj);

            // Try to find and set name text
            var nameText = itemObj.transform.Find("InfoPanel/GroupNameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = group.groupName;
                Debug.Log($"  ✓ Set name to: {group.groupName}");
            }
            else
            {
                Debug.LogError($"  ✗ Could not find GroupNameText in {itemObj.name}");

                // Debug: List all children
                Debug.Log($"  Available children in {itemObj.name}:");
                foreach (Transform child in itemObj.transform)
                {
                    Debug.Log($"    - {child.name}");
                }
            }

            // Try to find and set member count
            var memberCountText = itemObj.transform.Find("InfoPanel/MemberCountText")?.GetComponent<TextMeshProUGUI>();
            if (memberCountText != null)
            {
                memberCountText.text = $"{group.members.Count} members";
                Debug.Log($"  ✓ Set member count to: {group.members.Count}");
            }
            else
            {
                Debug.LogWarning($"  ⚠ Could not find MemberCountText");
            }

            // Try to find and set last activity
            var lastActivityText = itemObj.transform.Find("InfoPanel/LastActivityText")?.GetComponent<TextMeshProUGUI>();
            if (lastActivityText != null)
            {
                TimeSpan timeSince = DateTime.Now - group.lastActivity;
                string timeText = timeSince.TotalDays < 1
                    ? "Active today"
                    : $"Active {(int)timeSince.TotalDays} days ago";
                lastActivityText.text = timeText;
                Debug.Log($"  ✓ Set activity to: {timeText}");
            }
            else
            {
                Debug.LogWarning($"  ⚠ Could not find LastActivityText");
            }

            // Current indicator
            var currentIndicator = itemObj.transform.Find("CurrentIndicator")?.gameObject;
            if (currentIndicator != null)
            {
                bool isCurrent = group.groupId == currentGroup?.groupId;
                currentIndicator.SetActive(isCurrent);
                Debug.Log($"  ✓ Set current indicator: {isCurrent}");
            }
            else
            {
                Debug.LogWarning($"  ⚠ Could not find CurrentIndicator");
            }

            // Select button
            var selectButton = itemObj.GetComponent<Button>();
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(async () => await SwitchToGroup(group.groupId));
                Debug.Log($"  ✓ Button listener added");
            }
            else
            {
                Debug.LogError($"  ✗ No Button component on root!");
            }
        }

        private void ClearGroupListItems()
        {
            foreach (var item in spawnedGroupListItems)
            {
                if (item != null)
                    Destroy(item);
            }
            spawnedGroupListItems.Clear();
        }

        // =====================================================================
        // DIALOG ACTIONS
        // =====================================================================

        private void OpenCreateGroupDialog()
        {
            if (createGroupDialog != null)
            {
                string currentUserId = PlayerPrefs.GetString("UserId", "");
                createGroupDialog.ShowDialog(currentUserId);
                createGroupDialog.OnGroupCreated += OnGroupCreated;
            }
            else
            {
                ShowMessage("Create group dialog not available", MessageType.Error);
            }
        }

        private async void OnGroupCreated(Group newGroup)
        {
            var groupManager = GroupManager.Instance;
            if (groupManager != null)
            {
                // Group is already created by dialog, just switch to it
                await groupManager.SetCurrentGroupAsync(newGroup.groupId);
                ShowMessage($"Created group: {newGroup.groupName}", MessageType.Success);
                RefreshContent();
            }
        }

        private void OpenEditGroupDialog()
        {
            if (editGroupDialog != null && currentGroup != null)
            {
                editGroupDialog.ShowDialog(currentGroup);
                editGroupDialog.OnGroupUpdated += OnGroupUpdated;
            }
            else
            {
                ShowMessage("Edit group dialog not available", MessageType.Error);
            }
        }

        private async void OnGroupUpdated(Group updatedGroup)
        {
            var groupManager = GroupManager.Instance;
            if (groupManager != null)
            {
                await groupManager.UpdateGroupAsync(updatedGroup);
                ShowMessage($"Updated group: {updatedGroup.groupName}", MessageType.Success);
                RefreshContent();
            }
        }

        private void ConfirmDeleteGroup()
        {
            if (confirmDeleteDialog != null && currentGroup != null)
            {
                confirmDeleteDialog.ShowDialog(
                    $"Delete {currentGroup.groupName}?",
                    $"Are you sure you want to delete {currentGroup.groupName}? All inventory data for this group will be lost. This action cannot be undone.",
                    onConfirm: DeleteCurrentGroup,
                    onCancel: null
                );
            }
        }

        private async void DeleteCurrentGroup()
        {
            if (currentGroup == null) return;

            var groupManager = GroupManager.Instance;
            if (groupManager != null)
            {
                string groupName = currentGroup.groupName;
                await groupManager.DeleteGroupAsync(currentGroup.groupId);
                ShowMessage($"Deleted group: {groupName}", MessageType.Success);
                RefreshContent();
            }
        }

        private void OpenAddMemberDialog()
        {
            Debug.Log("entered open add member dialog");
            if (addMemberDialog != null && currentGroup != null)
            {
                addMemberDialog.ShowDialog(currentGroup.groupId);
                Debug.Log("ShowDialog finished");
                addMemberDialog.OnMemberAdded += OnMemberAdded;
                Debug.Log("member added event addition done");
            }
            else
            {
                Debug.Log($"Add member dialog not available {MessageType.Error}");
                ShowMessage("Add member dialog not available", MessageType.Error);
            }
        }

        private async void OnMemberAdded(string groupId, string userId, GroupPermission permission)
        {
            var groupManager = GroupManager.Instance;
            if (groupManager != null)
            {
                await groupManager.AddMemberToGroupAsync(groupId, userId, permission);
                ShowMessage("Member added to group", MessageType.Success);
                RefreshMembersDisplay();
            }
        }

        private void ConfirmRemoveMember(GroupMember member)
        {
            if (confirmDeleteDialog != null)
            {
                var userManager = UserManager.Instance;
                string userName = "this member";
                if (userManager != null)
                {
                    var userInfo = userManager.GetUsers().FirstOrDefault(u => u.userId == member.userId);
                    if (userInfo.userName != null)
                        userName = userInfo.userName.ToString();
                }
                else
                {
                    Debug.LogError("[GroupManager] UserManager.Instance is NULL!");
                }

                confirmDeleteDialog.ShowDialog(
                    $"Remove {userName}?",
                    $"Are you sure you want to remove {userName} from this group?",
                    onConfirm: () => RemoveMember(member),
                    onCancel: null
                );
            }
        }

        private async void RemoveMember(GroupMember member)
        {
            if (currentGroup == null) return;

            var groupManager = GroupManager.Instance;
            if (groupManager != null)
            {
                await groupManager.RemoveMemberFromGroupAsync(currentGroup.groupId, member.userId);
                ShowMessage("Member removed from group", MessageType.Success);
                RefreshMembersDisplay();
            }
        }

        private void OpenChangePermissionDialog(GroupMember member)
        {
            // TODO: Create a permission change dialog
            // For now, just cycle through permissions
            CyclePermission(member);
        }

        private async void CyclePermission(GroupMember member)
        {
            var groupManager = GroupManager.Instance;
            if (groupManager == null || currentGroup == null) return;

            // Cycle: Viewer → Member → Editor → Moderator → Admin → Viewer
            GroupPermission newPermission = member.permission switch
            {
                GroupPermission.Viewer => GroupPermission.Member,
                GroupPermission.Member => GroupPermission.Editor,
                GroupPermission.Editor => GroupPermission.Moderator,
                GroupPermission.Moderator => GroupPermission.Admin,
                GroupPermission.Admin => GroupPermission.Viewer,
                _ => GroupPermission.Member
            };

            await groupManager.UpdateMemberPermissionAsync(currentGroup.groupId, member.userId, newPermission);
            ShowMessage($"Permission changed to {newPermission}", MessageType.Success);
            RefreshMembersDisplay();
        }

        private void OpenAddCharacterDialog()
        {
            if (addCharacterDialog != null && currentGroup != null)
            {
                string currentUserId = PlayerPrefs.GetString("UserId", "");
                addCharacterDialog.ShowDialog(currentGroup.groupId, currentUserId);
                addCharacterDialog.OnCharacterAdded += OnCharacterAdded;
            }
            else
            {
                ShowMessage("Add character dialog not available", MessageType.Error);
            }
        }

        private async void OnCharacterAdded(string groupId, string userId, string characterId)
        {
            var groupManager = GroupManager.Instance;
            if (groupManager != null)
            {
                await groupManager.AddCharacterToGroupAsync(groupId, userId, characterId);
                ShowMessage("Character added to group", MessageType.Success);
                RefreshCharactersDisplay();
            }
        }

        private void ConfirmRemoveCharacter(PlayerCharacter character)
        {
            if (confirmDeleteDialog != null)
            {
                confirmDeleteDialog.ShowDialog(
                    $"Remove {character.characterName}?",
                    $"Are you sure you want to remove {character.characterName} from this group? Their items will remain in group inventory.",
                    onConfirm: () => RemoveCharacter(character),
                    onCancel: null
                );
            }
        }

        private async void RemoveCharacter(PlayerCharacter character)
        {
            if (currentGroup == null) return;

            var groupManager = GroupManager.Instance;
            if (groupManager != null)
            {
                await groupManager.RemoveCharacterFromGroupAsync(currentGroup.groupId, character.characterId);
                ShowMessage($"Removed {character.characterName} from group", MessageType.Success);
                RefreshCharactersDisplay();
            }
        }

        private async System.Threading.Tasks.Task SwitchToGroup(string groupId)
        {
            var groupManager = GroupManager.Instance;
            if (groupManager != null)
            {
                await groupManager.SetCurrentGroupAsync(groupId);
                ShowMessage("Switched group", MessageType.Success);

                // Close group list panel
                if (groupListPanel != null)
                    groupListPanel.SetActive(false);

                RefreshContent();
            }
        }
        private void OpenCreateUserDialog()
        {
            if (createUserDialog != null)
            {
                createUserDialog.ShowDialog();
                createUserDialog.OnUserCreated += OnUserCreated;
            }
            else
            {
                ShowMessage("Create user dialog not available", MessageType.Error);
            }
        }

        private void OnUserCreated(SerializableUserInfo newUser)
        {
            ShowMessage($"Created user: {newUser.userName}", MessageType.Success);

            // Optionally refresh members display if needed
            Debug.Log($"[GroupManagement] New user created: {newUser.userName}");
        }

        // =====================================================================
        // EVENT HANDLERS
        // =====================================================================

        private void OnGroupsChanged(List<Group> groups)
        {
            Debug.Log($"[GroupManagement] OnGroupsChanged called with {groups?.Count ?? 0} groups");

            // Only refresh if page is active AND properly initialized
            if (gameObject.activeInHierarchy && membersContainer != null && charactersContainer != null)
            {
                try
                {
                    RefreshContent();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GroupManagement] RefreshContent failed: {e.Message}");
                    Debug.LogError(e.StackTrace);
                }
            }
            else
            {
                Debug.Log("[GroupManagement] Skipping refresh - page not ready or not active");
            }
        }

        private void OnCurrentGroupChanged(Group newCurrentGroup)
        {
            currentGroup = newCurrentGroup;
            if (gameObject.activeInHierarchy)
            {
                RefreshContent();
            }
        }
        
        // =====================================================================
        // CLEANUP
        // =====================================================================

        protected override void ShowMessage(string message, MessageType type)
        {
            // TODO: Show actual UI toast/notification
            base.ShowMessage(message, type);
        }

        private void OnDestroy()
        {
            var groupManager = GroupManager.Instance;
            if (groupManager != null)
            {
                groupManager.OnGroupsChanged -= OnGroupsChanged;
                groupManager.OnCurrentGroupChanged -= OnCurrentGroupChanged;
            }

            if (createGroupDialog != null)
                createGroupDialog.OnGroupCreated -= OnGroupCreated;

            if (editGroupDialog != null)
                editGroupDialog.OnGroupUpdated -= OnGroupUpdated;

            if (addMemberDialog != null)
                addMemberDialog.OnMemberAdded -= OnMemberAdded;

            if (addCharacterDialog != null)
                addCharacterDialog.OnCharacterAdded -= OnCharacterAdded;

            if (createUserDialog != null)
                createUserDialog.OnUserCreated -= OnUserCreated;

            ClearMemberCards();
            ClearCharacterEntries();
            ClearGroupListItems();
        }
    }
}
