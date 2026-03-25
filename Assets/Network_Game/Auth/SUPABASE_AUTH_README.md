# Supabase Auth & Player Data Integration

## Overview

This integration provides:
- **Email/password authentication** via Supabase Auth
- **Cloud-synced player data** (progression, stats, unlocks)
- **Hybrid persistence** (local disk + cloud with cloud-first on login)
- **JWT session management** with automatic token refresh

## Database Schema

### Tables Created

| Table | Purpose |
|-------|---------|
| `player_profiles` | Player identity linked to auth.users |
| `player_game_data` | Persistent progression (HP, level, XP, stats) |
| `player_runtime_state` | Session health/position |

### RPC Functions

| Function | Purpose |
|----------|---------|
| `upsert_player_game_data` | Save player data to cloud |
| `get_player_game_data` | Load player data from cloud |
| `create_player_profile` | Link auth user to player profile |

## Components

### SupabaseAuthService
Handles JWT authentication, session persistence, and player profile creation.

```csharp
// Login
await SupabaseAuthService.Instance.LoginAsync("player@example.com", "password");

// Register
await SupabaseAuthService.Instance.RegisterAsync("player@example.com", "password", "PlayerName");

// Logout
await SupabaseAuthService.Instance.LogoutAsync();
```

### SupabasePlayerDataProvider
Syncs PlayerGameData to Supabase cloud storage.

```csharp
// Load from cloud (called automatically on login)
var data = await SupabasePlayerDataProvider.Instance.LoadFromCloudAsync(playerKey);

// Save to cloud (called automatically on data changes)
await SupabasePlayerDataProvider.Instance.SaveToCloudAsync(playerKey, data);
```

### AuthUIManager
Simple UI for login/register with email/password or guest mode.

## Setup Instructions

### 1. Configure Supabase URL

In Unity Inspector, set on both services:
- **Supabase URL**: `http://127.0.0.1:54321` (local) or your project URL
- **Anon Key**: Your project's anon/public key

### 2. Add to Scene

Create a GameObject with:
1. **SupabaseAuthService** (persists across scenes)
2. **SupabasePlayerDataProvider** (persists across scenes)
3. **AuthUIManager** (login UI panel)

### 3. UI Setup

Create a Canvas with:
- InputField (Email)
- InputField (Password) 
- InputField (Player Name - for register)
- Button (Login)
- Button (Register)
- Button (Guest - optional)
- Text (Status)

Assign all references in AuthUIManager inspector.

## Player Data Flow

```
1. Player logs in via AuthUIManager
   └─> SupabaseAuthService validates with Supabase Auth
   └─> JWT tokens stored in PlayerPrefs
   └─> Player profile created/linked

2. Player connects to game
   └─> PlayerDataManager.TryLoadOrCreatePlayerData()
   └─> Tries Supabase first (if authenticated)
   └─> Falls back to local disk
   └─> Creates new if neither exists

3. During gameplay
   └─> PlayerDataManager saves to disk periodically
   └─> SupabasePlayerDataProvider syncs to cloud
   └─> On death/level up: immediate cloud sync

4. Player disconnects
   └─> Final save to disk
   └─> Final cloud sync
```

## Security Notes

- **RLS enabled** on all tables - players can only access their own data
- **JWT tokens** stored in PlayerPrefs (not secure, but convenient)
- **Service role key** only used server-side (in DialoguePersistenceGateway)
- **Anon key** is safe to embed in client

## Testing

### Local Development
```bash
# Start Supabase local
npx supabase start

# Check status
npx supabase status
```

### Test Accounts
Create test accounts via SQL:
```sql
-- Or use the Unity AuthUIManager to register
```

## Migration from Local Auth

The system is backward compatible:
- Existing local JSON saves continue to work
- On first Supabase login, local data is preserved
- Cloud data takes precedence when available

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Login fails | Check Supabase URL and Anon Key |
| Cloud sync not working | Check auth state and network |
| Data not persisting | Check disk permissions (local) or RLS policies (cloud) |
| Missing player_key | Ensure `create_player_profile` RPC exists |
