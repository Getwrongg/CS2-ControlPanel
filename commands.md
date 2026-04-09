# Counter-Strike 2 (CS2) RCON Command Reference

A single-file README-style reference for CS2 RCON/server commands.

---

## Basic RCON Usage

```cfg
rcon_password "YOUR_PASSWORD"
rcon mp_restartgame 1
```

Alternative:
```cfg
mp_restartgame 1
```

```cfg
!rcon mp_restartgame 1
```

---

## Server Settings

```cfg
hostname "My CS2 Server"
sv_password ""
sv_lan 0
sv_region 255
game_type 0
game_mode 1
mapgroup mg_active
log on
exec server.cfg
```

---

## Match Control

```cfg
mp_restartgame 1
mp_warmup_start
mp_warmup_end
mp_match_end_restart 1
mp_pause_match
mp_unpause_match
mp_halftime
mp_swapteams
```

---

## Round Settings

```cfg
mp_roundtime 60
mp_roundtime_defuse 60
mp_freezetime 0
mp_buytime 9999
mp_c4timer 40
mp_maxrounds 30
mp_timelimit 0
```

---

## Money

```cfg
mp_startmoney 16000
mp_maxmoney 60000
mp_afterroundmoney 16000
mp_playercashawards 1
mp_teamcashawards 1
```

---

## Buy Settings

```cfg
mp_buy_anywhere 1
mp_buytime 9999
mp_weapons_allow_typecount -1
```

---

## Team / Player

```cfg
mp_limitteams 0
mp_autoteambalance 0
mp_forcecamera 0
mp_respawn_on_death_t 0
mp_respawn_on_death_ct 0
mp_friendlyfire 0
```

---

## Bots

```cfg
bot_add
bot_add_t
bot_add_ct
bot_kick
bot_kill
bot_stop 1
bot_freeze 1
bot_dont_shoot 1
bot_mimic 1
bot_place
bot_quota 0
```

---

## Practice

```cfg
sv_cheats 1
sv_infinite_ammo 1
ammo_grenade_limit_total 5
mp_drop_knife_enable 1
mp_weapons_allow_map_placed 1
```

---

## Grenades

```cfg
sv_grenade_trajectory 1
sv_grenade_trajectory_time 10
sv_showimpacts 1
sv_showimpacts_time 10
sv_rethrow_last_grenade
```

---

## Weapons

```cfg
give weapon_ak47
give weapon_m4a1
give weapon_awp
give weapon_deagle
give weapon_glock
give weapon_usp_silencer
give weapon_hegrenade
give weapon_flashbang
give weapon_smokegrenade
give weapon_molotov
give item_assaultsuit
```

---

## Maps

```cfg
map de_dust2
map de_mirage
map de_inferno
changelevel de_nuke
host_workshop_map 1234567890
```

---

## Respawn / DM

```cfg
mp_respawn_on_death_t 1
mp_respawn_on_death_ct 1
mp_ignore_round_win_conditions 1
```

---

## Voice

```cfg
sv_voiceenable 1
sv_alltalk 1
sv_deadtalk 1
```

---

## Voting

```cfg
sv_allow_votes 1
sv_vote_issue_kick_allowed 1
sv_vote_issue_restart_game_allowed 1
```

---

## Network

```cfg
sv_minrate 128000
sv_maxrate 0
sv_minupdaterate 128
sv_maxupdaterate 128
```

---

## TV

```cfg
tv_enable 1
tv_delay 0
```

---

## Debug

```cfg
noclip
god
cl_showpos 1
```

---

## Discovery

```cfg
find mp_
find sv_
find bot_
cvarlist
```

---

## 1v1 Config

```cfg
bot_kick
mp_limitteams 0
mp_autoteambalance 0
mp_roundtime_defuse 60
mp_freezetime 0
mp_buytime 9999
mp_buy_anywhere 1
mp_startmoney 16000
mp_respawn_on_death_t 0
mp_respawn_on_death_ct 0
mp_restartgame 1
```

---

## Smoke Practice

```cfg
bot_kick
sv_cheats 1
mp_roundtime_defuse 60
mp_freezetime 0
mp_buytime 9999
mp_buy_anywhere 1
sv_infinite_ammo 1
ammo_grenade_limit_total 5
sv_grenade_trajectory 1
sv_showimpacts 1
mp_restartgame 1
```

---

## Tip

Use:
```cfg
find mp_
find sv_
cvarlist
```

to see everything available on your server build.
