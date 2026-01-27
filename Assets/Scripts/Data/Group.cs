using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a gaming group/party/campaign
/// Contains members, permissions, and inventory
/// </summary>
[System.Serializable]
public class Group
{
    public string groupId;
    public string groupName;
    public string description;
    public string avatarSpriteName;  // Reference to avatar sprite

    public string creatorUserId;     // Who created this group
    public DateTime dateCreated;
    public DateTime lastActivity;

    public List<GroupMember> members;
    public GroupSettings settings;

    public Group()
    {
        groupId = Guid.NewGuid().ToString();
        groupName = "New Campaign";
        description = "";
        avatarSpriteName = "Group_Default";
        dateCreated = DateTime.Now;
        lastActivity = DateTime.Now;
        members = new List<GroupMember>();
        settings = new GroupSettings();

    }

    public Group(string name, string creatorId)
    {
        groupId = Guid.NewGuid().ToString();
        groupName = name;
        description = "";
        avatarSpriteName = "Group_Default";
        creatorUserId = creatorId;
        dateCreated = DateTime.Now;
        lastActivity = DateTime.Now;
        members = new List<GroupMember>();
        settings = new GroupSettings();

        // Add creator as admin
        AddMember(creatorId, GroupPermission.Admin);
    }

    public void AddMember(string userId, GroupPermission permission = GroupPermission.Member)
    {
        // Check if already member
        if (members.Exists(m => m.userId == userId))
        {
            Debug.LogWarning($"User {userId} is already a member of group {groupName}");
            return;
        }

        members.Add(new GroupMember
        {
            userId = userId,
            permission = permission,
            joinedDate = DateTime.Now
        });

        lastActivity = DateTime.Now;
    }

    public void RemoveMember(string userId)
    {
        members.RemoveAll(m => m.userId == userId);
        lastActivity = DateTime.Now;
    }

    public GroupMember GetMember(string userId)
    {
        return members.Find(m => m.userId == userId);
    }

    public bool HasMember(string userId)
    {
        return members.Exists(m => m.userId == userId);
    }

    public bool UserHasPermission(string userId, GroupPermission requiredPermission)
    {
        var member = GetMember(userId);
        if (member == null)
        {
            Debug.Log("[group] unable to find member");
            return false;
        }

        // Admin has all permissions
        if (member.permission == GroupPermission.Admin) return true;

        return member.permission >= requiredPermission;
    }
}

/// <summary>
/// Represents a member of a group
/// </summary>
[System.Serializable]
public class GroupMember
{
    public string userId;
    public GroupPermission permission;
    public DateTime joinedDate;
    public List<string> characterIds;  // Characters this user has in this group

    public GroupMember()
    {
        characterIds = new List<string>();
        joinedDate = DateTime.Now;
    }
}

/// <summary>
/// Permission levels for group members
/// </summary>
public enum GroupPermission
{
    Viewer = 0,      // Can only view inventory
    Member = 1,      // Can claim items, add notes
    Editor = 2,      // Can add/remove items, modify quantities
    Moderator = 3,   // Can manage members (except admin)
    Admin = 4        // Full control
}

/// <summary>
/// Settings for a group
/// </summary>
[System.Serializable]
public class GroupSettings
{
    public bool allowMembersToInvite;
    public bool requireApprovalForNewMembers;
    public bool allowMembersToAddItems;
    public int maxMembers;

    public GroupSettings()
    {
        allowMembersToInvite = false;
        requireApprovalForNewMembers = true;
        allowMembersToAddItems = true;
        maxMembers = 10;
    }
}

/// <summary>
/// Serializable version for storage
/// </summary>
[System.Serializable]
public class SerializableGroup
{
    public string groupId;
    public string groupName;
    public string description;
    public string avatarSpriteName;
    public string creatorUserId;
    public string dateCreated;
    public string lastActivity;
    public List<SerializableGroupMember> members;
    public GroupSettings settings;

    public static SerializableGroup FromGroup(Group group)
    {
        return new SerializableGroup
        {
            groupId = group.groupId,
            groupName = group.groupName,
            description = group.description,
            avatarSpriteName = group.avatarSpriteName,
            creatorUserId = group.creatorUserId,
            dateCreated = group.dateCreated.ToString("O"),
            lastActivity = group.lastActivity.ToString("O"),
            members = group.members.ConvertAll(SerializableGroupMember.FromGroupMember),
            settings = group.settings
        };
    }

    public Group ToGroup()
    {
        return new Group
        {
            groupId = groupId,
            groupName = groupName,
            description = description,
            avatarSpriteName = avatarSpriteName,
            creatorUserId = creatorUserId,
            dateCreated = DateTime.TryParse(dateCreated, out var dc) ? dc : DateTime.Now,
            lastActivity = DateTime.TryParse(lastActivity, out var la) ? la : DateTime.Now,
            members = members?.ConvertAll(m => m.ToGroupMember()) ?? new List<GroupMember>(),
            settings = settings ?? new GroupSettings()
        };
    }
}

[System.Serializable]
public class SerializableGroupMember
{
    public string userId;
    public GroupPermission permission;
    public string joinedDate;
    public List<string> characterIds;

    public static SerializableGroupMember FromGroupMember(GroupMember member)
    {
        return new SerializableGroupMember
        {
            userId = member.userId,
            permission = member.permission,
            joinedDate = member.joinedDate.ToString("O"),
            characterIds = new List<string>(member.characterIds)
        };
    }

    public GroupMember ToGroupMember()
    {
        return new GroupMember
        {
            userId = userId,
            permission = permission,
            joinedDate = DateTime.TryParse(joinedDate, out var jd) ? jd : DateTime.Now,
            characterIds = characterIds ?? new List<string>()
        };
    }
}