# ArrowOut

ArrowOut is a puzzle game that runs in the browser. You get a board packed full of arrows. Tap one and it
slides the way it points. If nothing is in front of it, it leaves the board. If it hits another arrow, it
bounces back and you lose a heart. Clear the whole board and you win.

If you've played *Arrows – Puzzle Escape* on your phone, you know the idea. This is my own take on it, with
its own name, look and randomly generated boards. Nothing (graphics, levels, branding) is copied from the
mobile game.

It's built with ASP.NET Core MVC on .NET 10, Entity Framework Core with SQL Server, ASP.NET Core Identity for
accounts, Bootstrap 5, and plain JavaScript that draws the board as SVG. Colour themes are stored as JSON
files, and there's an optional hook to send gameplay stats to PostHog.

## Getting it running

You'll need:

- the .NET 10 SDK
- Visual Studio 2026, Rider, or just the `dotnet` command line
- SQL Server. LocalDB (comes with Visual Studio) is enough. SSMS is handy if you want to look at the data.
- Node 20 or newer, but only if you want to run the JavaScript tests

Clone it and restore the packages:

```bash
git clone <repo> && cd ArrowOut
dotnet restore
```

By default the app connects to LocalDB:

```
Server=(localdb)\MSSQLLocalDB;Database=ArrowOut;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

If your SQL Server lives somewhere else, don't edit `appsettings.json`. Put your own connection string in
user secrets instead:

```bash
cd ArrowOut/ArrowOut.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=ArrowOut;Trusted_Connection=True;TrustServerCertificate=True"
```

Then start it:

```bash
dotnet run --project ArrowOut/ArrowOut.Web
```

In Visual Studio, set ArrowOut.Web as the startup project and press F5.

The first time it starts, the app creates the database, adds the roles and two accounts, and copies the four
built-in colour themes into `App_Data/storage/themes`. It's safe to restart as often as you like, because
nothing gets created twice. There are no boards in the database up front. Every board is generated the moment
someone starts a game.

### Accounts

In Development you get two ready-made accounts:

| Who | E-mail | Password |
|---|---|---|
| Admin | `admin@arrowout.local` | `Admin#12345` |
| Demo player | `demo@arrowout.local` | `Demo#12345` |

The admin can open the Administration area. The demo account is a normal player, which is useful for showing
the game to someone. Admins don't show up on the leaderboard, so I can test as much as I want without
messing up the rankings.

These passwords live in `appsettings.Development.json` and are only meant for local use. Anywhere else, set the
admin password through a secret or an environment variable:

```bash
dotnet user-secrets set "Seed:AdminPassword" "<strong password>"
# or: export Seed__AdminPassword="<strong password>"
```

If no admin exists yet and no password is set, the app writes a warning to the log and starts anyway without
an admin. The demo account only gets created when both `Seed:DemoEmail` and `Seed:DemoPassword` are set, which
by default is only in Development.

### Confirming e-mail addresses

New players have to confirm their e-mail before they can sign in. After signing up they get an e-mail with
a link, and clicking it activates the account and signs them in. The link works for 24 hours. If it got lost,
there's a "Send it again" link on the "Check your inbox" page and on the sign-in page. The admin and demo
accounts are created already confirmed.

To actually send e-mails, the app needs an SMTP server. Any mail provider works. With Gmail:

1. Turn on 2-Step Verification for your Google account.
2. Go to <https://myaccount.google.com/apppasswords> and create an app password. You get 16 letters.
   Your normal Gmail password won't work here.
3. Save the settings in user secrets, so the password never ends up in the code:

```bash
cd ArrowOut/ArrowOut.Web
dotnet user-secrets set "Email:SmtpHost" "smtp.gmail.com"
dotnet user-secrets set "Email:SmtpPort" "587"
dotnet user-secrets set "Email:UserName" "you@gmail.com"
dotnet user-secrets set "Email:Password" "<the 16-letter app password>"
dotnet user-secrets set "Email:FromAddress" "you@gmail.com"
```

Restart the app and sign up with a real address to try it. For Outlook use `smtp-mail.outlook.com`, port 587.

Without these settings nothing is sent. The e-mail (including the link) is written to the console log
instead, and in Development the "Check your inbox" page also shows the link, so you can test sign-up without
a mailbox. On a real server you have to set up SMTP, otherwise nobody can finish signing up.

### Staying on .NET 8 or Visual Studio 2022

VS 2022 only officially goes up to .NET 9. To run on .NET 8:

1. In `Directory.Build.props`, change the target framework to `net8.0`.
2. In `Directory.Packages.props`, set `AspNetCoreVersion` and `EfCoreVersion` to `8.0.x`, and
   `Microsoft.Extensions.Http.Resilience` to `8.x`.
3. In `Program.cs`, delete the `ConfigureWarnings(...)` line.
4. Recreate the migration (see the database section below).

## How to play

Tap or click an arrow to send it off. The keyboard works too: Tab moves between arrows and Enter or Space taps
the selected one. A few shortcuts:

- H gives you a hint
- R restarts the board
- G turns the grid on or off
- `+`, `-` and `0` zoom in, zoom out and reset the zoom

Each attempt gives you 3 hearts and 3 hints. Your stars depend on how many times you crashed: none gets 3
stars, one gets 2, and anything more gets 1. Hints don't cost you anything.

The big boards don't fit on screen, so you can zoom with the mouse wheel or by pinching, and drag to move
around. Dragging never counts as a tap, so you won't send an arrow flying by accident.

## Games

Press "Start game" on the home page and you get a brand-new random board. The difficulty is random too, so
you never know if it's going to be Easy, Normal or Hard until the board shows up:

| Kind | Board size | Arrows | Arrow length | Points for a win |
|---|---|---|---|---|
| Easy | 16×16 | about 40 (at least 32) | 3 to 12 cells | 1 |
| Normal | 41×41 to 43×43 | about 200 (at least 180) | 4 to 20 cells | 4 |
| Hard | 66×66 to 82×82 | 400 to about 700 | 4 to 24 cells | 10 |

Next to it there's a "Challenge game" button. That's its own mode with much bigger boards, worth 20 points a
win, and it's never picked by "Start game". The site doesn't say anything else about it on purpose, so it's a
surprise. The exact board sizes are in `ChallengeGenerator`. After a Challenge game, "Start new game" gives you
another Challenge game.

You only get the points for a board the first time you beat it. Replaying a board you've already won is fine,
but it won't earn you any more points. Otherwise people could just farm the easy ones.

A board belongs to the player who started it. If someone else tries to open it by guessing the id in the URL,
they get a "not found" page, the same as for a board that doesn't exist.

The home page shows how many boards you've played and won, your stars and points, and a button to jump back
into your last unfinished board.

## Leaderboard

There's a separate leaderboard for Easy, Normal, Hard and Challenge games. Players are ranked by points first, then by number
of wins, then by stars, and finally by who made the fewest mistakes. It never shows e-mail addresses. If
someone hasn't set a display name, only the first two letters of their e-mail are shown, followed by `***`.
Admin accounts are left out completely.

## How the game works under the hood

Every arrow is a chain of cells that can bend, so a board looks like a maze of snakes. Every single cell is
covered, so each arrow starts out boxed in by others. When you tap an arrow it moves like a train: the head goes
straight ahead and the rest of the body follows along its own path. In other words, an arrow can escape only
when every cell in front of its head is empty.

That gives the game a nice property. Removing an arrow only ever frees up cells and never blocks anything new.
So once an arrow is free, it stays free for the rest of the game, and the order you remove free arrows in
doesn't change the final result. Because of that, the solver (`GreedySolver`) never has to backtrack: it keeps
removing free arrows until the board is empty or stuck. It also means hints are always safe, since any free
arrow is a correct next move. The hint picks the free arrow that unblocks the most other arrows.

Boards are generated backwards (`LevelGenerator`). The generator places arrows in the reverse of the order
you'd remove them in, and each new arrow needs a clear straight line from its head to the edge at the moment
it's placed. That's why every board is solvable. To be safe, each board still gets checked by the solver
afterwards. Two settings shape how boards look:

- `HugWalls` makes arrows grow along the walls and along their neighbours, so you get long arrows packed
  side by side instead of short random squiggles.
- `Tangle` (0 to 1) points arrows across the board instead of toward the nearest edge. Arrows then block each
  other in long chains, so you can't just clear the board from the outside in. Boards stay solvable at any
  value.

There's also no way to cheat by telling the server "I won". The browser plays the moves locally so the
animations feel instant, but when you finish, it sends the full list of your taps. The server replays them
on its own copy of the board (`GameReplayer`) and only records the win if the replay really ends with an empty
board. Taps on arrows that don't exist, taps after the board is already clear, and taps after you've run out
of hearts are all rejected.

## Looks

The site has a cartoon style, with thick outlines, chunky buttons and bouncy dialogs. The arrows look like
they were drawn with a soft black pencil on paper. On smaller boards they also get a bit of grain and
wobble. The Normal, Hard and Challenge boards skip that effect, because recalculating it for hundreds of arrows while
you pan around made scrolling laggy.

There are four built-in themes: Peach Morning, Cocoa Night, Mint and Sunset. The light/dark button in the menu
switches right away. For logged-in players the choice is saved on their account, and for visitors in a
cookie. Until you pick one, the site follows your device's light or dark setting. In dark mode the pencil turns
white so the arrows stay visible.

## Admin area

Admins get a separate area with:

- a dashboard with some totals
- a level editor where you can draw arrows on a grid, check solvability live, generate a random layout, and
  import or export levels as JSON. Players currently only play the random games, but the levels are still
  available through `/api/levels`.
- user management: search, lock and unlock, promote and demote, delete. You can't lock yourself out.
- theme upload. New themes are checked before they're saved. The built-in ones can't be deleted.

## Database

There's a single migration, `InitialCreate`, which sets up everything: the Identity tables, levels, arrows,
player progress and games.

To wipe everything and start fresh, drop the database. On the next start the app recreates it along with the
admin and demo accounts:

```bash
dotnet ef database drop --force --project ArrowOut/ArrowOut.Data --startup-project ArrowOut/ArrowOut.Web
```

If you change the entities, add a new migration and apply it:

```bash
dotnet tool install --global dotnet-ef   # only needed once
dotnet ef migrations add <Name> --project ArrowOut/ArrowOut.Data --startup-project ArrowOut/ArrowOut.Web
dotnet ef database update --project ArrowOut/ArrowOut.Data --startup-project ArrowOut/ArrowOut.Web
```

The database checks its own data too. Board sizes must be between 3 and 96, lives between 1 and 5, arrows
between 1 and 64 cells, and stars between 0 and 3. Level numbers are unique, and two arrows can't start on the
same cell. Deleting a user also deletes their games and progress.

A game board never changes after it's created, so I store it as one compact string instead of hundreds of
rows. A Hard board can have around 700 arrows, so that saves a lot. Admin levels get edited arrow by arrow, so
they keep a normal `Arrows` table.

## Settings

| Setting | Default | What it's for |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | LocalDB | Where the database is |
| `Seed:AdminEmail` / `Seed:AdminPassword` | see Accounts | The first admin account |
| `Seed:DemoEmail` / `Seed:DemoPassword` | empty (filled in for Development) | The demo player account |
| `FileStorage:RootPath` | `App_Data/storage` | Where theme files are kept |
| `Analytics:Enabled` | `false` | Turns on sending stats to PostHog |
| `Analytics:ApiKey` | empty | Your PostHog project key |
| `Analytics:Host` | `https://eu.i.posthog.com` | Which PostHog region to use |
| `Analytics:QueueCapacity` | `1000` | How many events can wait in the queue |
| `Email:SmtpHost` / `Email:SmtpPort` | empty / `587` | The mail server for confirmation e-mails |
| `Email:UserName` / `Email:Password` | empty | Login for the mail server (keep these in user secrets) |
| `Email:FromAddress` / `Email:FromName` | empty / `ArrowOut` | Who the e-mails come from |

About analytics: when it's turned on, the game sends an event when a game is created and another when it's
won (kind, number of arrows, mistakes, hints used, points). Events go into a small in-memory queue and are
sent in the background, so the game never waits on PostHog. If the queue fills up, the oldest events are
dropped. User ids are hashed before anything leaves the server, so PostHog never sees e-mail addresses. With
analytics turned off, nothing is sent at all.

## API

The game page talks to the server through a small JSON API. Everything except the leaderboard needs you to be
logged in. POST requests also need the anti-forgery token in an `X-CSRF-TOKEN` header, which the page's
scripts add for you.

| Method | Route | What it does |
|---|---|---|
| POST | `/game/new` | Starts a new board and redirects to it. No `kind` = random Easy/Normal/Hard, `kind=Challenge` = Challenge game |
| GET | `/game/{id}` | The game page for one of your boards |
| GET | `/leaderboard?kind=&page=` | The leaderboard page |
| POST | `/theme/mode` | Switches light or dark mode (form fields `mode` and `returnUrl`) |
| GET | `/api/games/{id}` | The board layout |
| POST | `/api/games/{id}/attempts` | Records that you started or restarted |
| POST | `/api/games/{id}/hint` | Asks for a hint for the current board state |
| POST | `/api/games/{id}/moves` | Checks what a tap would do, without saving anything |
| POST | `/api/games/{id}/completion` | Sends your taps so the server can check the win |
| GET | `/api/leaderboard?kind=&page=&pageSize=` | The leaderboard as JSON |
| GET | `/api/progress` | Your progress on the admin-made levels |
| GET/POST | `/api/levels/...` | The same actions as above, for the admin-made levels |

Errors from the API come back as standard `ProblemDetails` JSON, not as HTML pages. For example, if the replay
doesn't hold up, you get a 422.

## Security

A few things I paid attention to:

- All database access goes through Entity Framework, so there's no hand-written SQL to inject into.
- Razor escapes everything it outputs, and the scripts never put data into `innerHTML`. On top of that, the
  Content Security Policy only allows scripts from the site itself. Theme colours have to be proper hex codes
  before they end up in a style block.
- Every form and every POST request is protected against CSRF.
- The user id always comes from the login cookie, never from the request. Boards are loaded by id *and* owner.
  Wins are replayed on the server, and hints are counted there too.
- Theme files can only have safe names and can't escape the storage folder. Uploads are size-limited and must
  be JSON.
- After 5 wrong passwords an account is locked for 10 minutes. If an admin locks or demotes someone, it takes
  effect within a minute.
- The usual headers are set to stop the site being embedded in frames or having its content types sniffed.
  The light/dark switch only redirects back to pages on this site.

## Tests

```bash
dotnet test                               # C# tests (xUnit)
node --test tests/js/engine.test.mjs      # checks that the JS engine matches the C# one
```

The C# tests cover the game engine (moves, collisions, solver, hints, replay, generator), the game rules
(board sizes, points, replays, who can see which board), the leaderboard (ranking, hidden e-mails, no admins),
themes and file storage (including injection and path tricks), the admin level editor, and the controllers
and error handling. The JavaScript tests make sure the browser plays by exactly the same rules as the server.

## Project layout

```
ArrowOut.sln
├── Directory.Build.props        # shared build settings
├── Directory.Packages.props     # NuGet versions in one place
├── ArrowOut/
│   ├── ArrowOut.Game/           # the puzzle engine, no dependencies
│   ├── ArrowOut.Data/           # EF Core entities, DbContext, migration, seeding
│   ├── ArrowOut.Services/       # game logic, leaderboard, themes, analytics
│   └── ArrowOut.Web/            # MVC pages, API, admin area, CSS and JavaScript
└── tests/
    ├── ArrowOut.Tests/          # xUnit tests
    └── js/engine.test.mjs       # JavaScript engine tests
```

References only go one way: Web uses Services, Services uses Data, and Data uses Game. The Game project knows
nothing about databases or the web, which makes it easy to test on its own.
