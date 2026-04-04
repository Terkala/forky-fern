# Blob antagonist

blob-overmind-name = blob overmind
blob-overmind-desc = An incorporal mass looking for a place to root.

blob-role-briefing = You are the Blob overmind. Use the "Deploy nucleus" action in your action bar, then click a station floor tile to place your core and starting patch. After that, spread, gather bio, and overrun the station. Destroy all crew resistance.

blob-hud-title = [color=#9fdf9f]══ Blob hive ══[/color]
blob-hud-line-bio-evo = Bio: [color=white]{ $bio }[/color] / { $biomax }    Evo: [color=white]{ $evo }[/color]
blob-hud-line-tiles = Tiles: [color=white]{ $tiles }[/color]{ $capsuffix }
blob-hud-cap-suffix-none =  [color=#888](no spread cap)[/color]
blob-hud-cap-suffix =  / [color=white]{ $max }[/color] [color=#888]max[/color]
blob-hud-line-evo-progress = Next evo: [color=white]{ $next }[/color] tiles · Nuclei: [color=white]{ $nuclei }[/color]
blob-hud-need-deploy = [color=#c9a85c]Use "Deploy nucleus" on a floor tile to start.[/color]

blob-spread-at-cap = Can't spread further ({ $current } / { $max } tiles). Promote another nucleus to raise the cap.

blob-attack-blocked = You have no nearby blob with a clear path to that tile (can't attack through walls).

blob-already-deployed = You have already deployed.
blob-deploy-invalid-tile = You must deploy on solid station flooring.
blob-deploy-too-close = Something is wrong with adjacent tiles; choose another spot.
blob-deploy-success = The blob takes hold.

blob-not-enough-bio = Not enough bio points.
blob-not-enough-evo = Not enough evolution points.
blob-evo-failed = Can't purchase that right now.
blob-spread-not-adjacent = You can only spread next to existing blob tiles.
blob-spread-space = You can't spread into space without a bridge.
blob-spread-cooldown = The blob needs a moment before spreading again.
blob-locked = That evolution isn't unlocked yet.
blob-nucleus-cap = Maximum nuclei for now.

blob-verb-specialist-menu = Reshape blob tile
blob-specialist-radial-option = { $name } ({ $cost } bio)

blob-round-end-blob-major = The blob has consumed the station!
blob-round-end-crew-major = The blob has been eradicated.
blob-round-end-stats = Blob: {$tiles} tiles, {$bio} bio, {$evo} evo, {$nuclei} nuclei alive.
blob-round-end-overmind = Overmind: {$name} ({$user})

action-name-blob-deploy = Deploy nucleus
action-desc-blob-deploy = Place your nucleus and initial patch on a floor tile.

action-name-blob-color = Randomize tint
action-desc-blob-color = Pick a new random color for your hive overlay.

action-name-blob-spread = Spread
action-desc-blob-spread = Grow into an adjacent floor tile (costs bio).

action-name-blob-repair = Repair tile
action-desc-blob-repair = Heal one of your blob tiles.

action-name-blob-consume = Consume tile
action-desc-blob-consume = Remove one wrong blob cell for a partial refund.

action-name-blob-promote = Promote nucleus
action-desc-blob-promote = Turn a normal blob cell into an extra nucleus.

action-name-blob-ribosome = Ribosome cell
action-desc-blob-ribosome = Increases passive bio generation.

action-name-blob-lipid = Lipid cell
action-desc-blob-lipid = Storage cell (placeholder).

action-name-blob-mitochondria = Mitochondria
action-desc-blob-mitochondria = Regenerates nearby blob over time.

action-name-blob-membrane = Thick membrane
action-desc-blob-membrane = Tough defensive tile.

action-name-blob-firewall = Firewall membrane
action-desc-blob-firewall = Fire-resistant blob tile.

action-name-blob-devour = Devour item
action-desc-blob-devour = Destroy an item on or next to the blob.

action-name-blob-bridge = Bridge
action-desc-blob-bridge = Place plating on space next to the blob.

action-name-blob-launcher = Slime launcher
action-desc-blob-launcher = Turret tile (placeholder).

action-name-blob-plasmaphyll = Plasmaphyll
action-desc-blob-plasmaphyll = Plasma-draining structure (placeholder).

action-name-blob-ectothermid = Ectothermid
action-desc-blob-ectothermid = Heat-sinking structure (placeholder).

action-name-blob-reflective = Reflective membrane
action-desc-blob-reflective = May reflect energy fire.

action-name-blob-evo-gen = Evo: +generation
action-desc-blob-evo-gen = +2 bio generation per second per purchase.

action-name-blob-evo-quickspread = Evo: quick spread
action-desc-blob-evo-quickspread = Reduces spread cooldown.

action-name-blob-evo-spreadchance = Evo: spread chance
action-desc-blob-evo-spreadchance = Bonus spread rolls when you grow.

action-name-blob-evo-attack = Evo: attack
action-desc-blob-evo-attack = Stronger primary attack damage.

action-name-blob-evo-fireresist = Evo: fire resist
action-desc-blob-evo-fireresist = Halves heat damage to your tiles.

action-name-blob-evo-poisonresist = Evo: toxin resist
action-desc-blob-evo-poisonresist = Reduces poison-type damage to tiles.

action-name-blob-evo-unlock-devour = Unlock: Devour
action-desc-blob-evo-unlock-devour = Allows devouring items.

action-name-blob-evo-unlock-bridge = Unlock: Bridge
action-desc-blob-evo-unlock-bridge = Build floors in space and spread off-station.

action-name-blob-evo-unlock-launcher = Unlock: Launcher
action-desc-blob-evo-unlock-launcher = Build slime launcher tiles.

action-name-blob-evo-unlock-plasma = Unlock: Plasmaphyll
action-desc-blob-evo-unlock-plasma = Build plasmaphyll tiles.

action-name-blob-evo-unlock-ecto = Unlock: Ectothermid
action-desc-blob-evo-unlock-ecto = Build ectothermid tiles.

action-name-blob-evo-unlock-reflect = Unlock: Reflective
action-desc-blob-evo-unlock-reflect = Build reflective membranes.
