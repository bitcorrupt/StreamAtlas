using System;
using System.Net;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace StreamAtlas_System
{
    public class WebServer
    {
        private HttpListener _listener;
        private string _url = "http://localhost:8000/";
        private StreamAtlasDL _dl = new StreamAtlasDL();
        private static Dictionary<string, User> _sessions = new Dictionary<string, User>();

        public void Start()
        {
            _listener = new HttpListener(); _listener.Prefixes.Add(_url); _listener.Start();
            System.Diagnostics.Process.Start(_url);
            Console.WriteLine("StreamAtlas Server Running...");
            while (true) ProcessRequest(_listener.GetContext());
        }

        private void ProcessRequest(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request; var res = ctx.Response;
                string path = req.Url.AbsolutePath.ToLower();
                string method = req.HttpMethod;
                User user = GetUser(req);
                string html = "";

                if (path == "/login" && method == "POST")
                {
                    var d = Parse(req); var u = _dl.Login(d["u"]);
                    string k = Guid.NewGuid().ToString(); _sessions[k] = u;
                    res.AddHeader("Set-Cookie", $"sid={k}; Path=/"); res.Redirect("/");
                }
                else if (path == "/logout") { res.AddHeader("Set-Cookie", "sid=; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT"); res.Redirect("/"); }
                else if (path == "/add" && method == "POST")
                {
                    var d = Parse(req);
                    _dl.AddMedia(d["type"], d["t"], d["d"], int.Parse(d["y"]), d["e1"], d["e2"], d["genres"]);
                    res.Redirect("/");
                }
                else if (path == "/wishlist_toggle")
                {
                    if (user != null) _dl.ToggleWishlist(user.UserId, int.Parse(req.QueryString["id"]), req.QueryString["type"]);
                    res.Redirect("/");
                }
                else if (path == "/review" && method == "POST")
                {
                    var d = Parse(req);
                    if (user != null) _dl.AddReview(user.UserId, int.Parse(d["id"]), d["type"], int.Parse(d["rating"]), d["txt"]);
                    res.Redirect("/");
                }
                else if (path == "/search") html = PageSearch(user, req);
                else if (path == "/dashboard") html = PageDashboard(user);
                else html = PageHome(user);

                byte[] b = Encoding.UTF8.GetBytes(html);
                res.ContentType = "text/html; charset=utf-8"; res.ContentLength64 = b.Length; res.OutputStream.Write(b, 0, b.Length); res.Close();
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); ctx.Response.Close(); }
        }

        string PageHome(User u)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(@"<div class='hero'>
                <div class='hero-bg'></div><div class='hero-grad'></div>
                <div class='hero-content'>
                    <span class='highlight-badge'>Now Streaming</span>
                    <h1 class='title'>STREAM<br>ATLAS</h1>
                    <p class='desc'>Dive into a universe of entertainment. Your next obsession is just a click away.</p>
                    <div class='btns'><a href='/search' class='btn play'>▶ Play Now</a><a href='/dashboard' class='btn glass'>ℹ Analytics</a></div>
                </div>
            </div>");
            sb.Append("<div class='main-container'>");
            sb.Append(RenderSection(u, "Trending Movies", _dl.GetHomeData("Movies")));
            sb.Append(RenderSection(u, "Binge-Worthy Series", _dl.GetHomeData("Series")));
            sb.Append(RenderSection(u, "Top Games", _dl.GetHomeData("Games")));
            sb.Append("</div>");
            return Layout("Home", u, sb.ToString());
        }

        string PageSearch(User u, HttpListenerRequest r)
        {
            var res = _dl.FilterMedia(r.QueryString["q"], r.QueryString["g"]);
            StringBuilder sb = new StringBuilder();
            sb.Append($@"<div class='search-header'><div class='glass-box'>
                    <h2>Find Your Next Favorite</h2>
                    <form action='/search' class='search-bar'>
                        <input name='q' placeholder='Search titles, actors...' value='{r.QueryString["q"]}'>
                        <input name='g' placeholder='Genre' class='sm' value='{r.QueryString["g"]}'>
                        <button>Search</button>
                    </form></div></div>");
            sb.Append("<div class='grid-container'>");
            if (res.Count == 0) sb.Append("<div class='empty-msg'>No results found.</div>");
            foreach (var m in res) sb.Append(RenderCard(u, m));
            sb.Append("</div>");
            return Layout("Search", u, sb.ToString());
        }

        string PageDashboard(User u)
        {
            var s = _dl.GetStats();
            return Layout("Dashboard", u, $@"<div class='dash-container'><h2>System Analytics</h2>
                <div class='stats-grid'>
                    <div class='stat-glass'><h3>{s.Users}</h3><span>Total Users</span></div>
                    <div class='stat-glass'><h3>{s.Movies}</h3><span>Movies</span></div>
                    <div class='stat-glass'><h3>{s.Series}</h3><span>Series</span></div>
                    <div class='stat-glass'><h3>{s.Games}</h3><span>Games</span></div>
                    <div class='stat-glass wide'><span class='label'>Highest Rated</span><h3 class='gold'>{s.TopMovie}</h3></div>
                </div></div>");
        }

        string RenderSection(User u, string t, List<MediaItem> items)
        {
            StringBuilder h = new StringBuilder();
            h.Append($"<div class='section'><h3>{t}</h3><div class='scroll-row'>");
            if (items.Count == 0) h.Append("<div class='empty-msg'>Coming Soon</div>");
            foreach (var i in items) h.Append(RenderCard(u, i));
            h.Append("</div></div>");
            return h.ToString();
        }

        string RenderCard(User u, MediaItem m)
        {
            string genre = m.Genres.Count > 0 ? m.Genres[0] : m.Type;
            string gradClass = m.Type == "Movie" ? "Movies" : (m.Type == "Game" ? "Games" : m.Type);
            string wishSymbol = "+"; string wishTitle = "Add to List";
            if (u != null && u.Wishlist.Contains(m.Id)) { wishSymbol = "✓"; wishTitle = "In List"; }

            return $@"<div class='glass-card' tabindex='0'>
                <div class='card-bg {gradClass}'></div>
                <div class='card-content'>
                    <div class='card-top'><span class='badge'>{genre}</span><span class='hd'>HD</span></div>
                    <div class='card-info'>
                        <h4>{m.Title}</h4>
                        <div class='meta-row'><span class='match'>98% Match</span><span>{m.Year}</span></div>
                        <div class='hover-details'><p>{m.Description}</p>
                            <div class='actions'><button class='play-btn'>▶ Play</button>
                            {(u != null ? $"<a href='/wishlist_toggle?id={m.Id}&type={m.Type}' class='icon-btn' title='{wishTitle}'>{wishSymbol}</a>" : "")}</div>
                            {(u != null ? $"<form action='/review' method='post' class='quick-review'><input type='hidden' name='id' value='{m.Id}'><input type='hidden' name='type' value='{m.Type}'><input name='txt' placeholder='Write a review...'><input name='rating' value='5' type='hidden'></form>" : "")}
                        </div>
                    </div>
                </div></div>";
        }

        string Layout(string t, User u, string c) => $@"<!DOCTYPE html>
        <html><head><meta charset='utf-8'><title>{t}</title>
        <link href='https://fonts.googleapis.com/css2?family=Outfit:wght@300;500;700;900&display=swap' rel='stylesheet'>
        <style>
            :root {{ --bg: #0f0f0f; --primary: #E50914; --glass: rgba(255, 255, 255, 0.05); --glass-border: 1px solid rgba(255, 255, 255, 0.1); --text: #fff; }}
            body {{ background: var(--bg); color: var(--text); font-family: 'Outfit', sans-serif; margin: 0; overflow-x: hidden; }}
            .nav {{ position: fixed; top: 0; width: 100%; height: 70px; display: flex; justify-content: space-between; align-items: center; padding: 0 5%; box-sizing: border-box; z-index: 1000; background: linear-gradient(to bottom, rgba(0,0,0,0.9), transparent); backdrop-filter: blur(5px); }}
            .brand {{ color: var(--primary); font-size: 1.8rem; font-weight: 900; text-decoration: none; text-shadow: 0 0 10px rgba(229,9,20,0.5); }}
            .nav-links a {{ color: #ccc; text-decoration: none; margin-left: 20px; font-weight: 500; transition: 0.3s; }}
            .nav-links a:hover {{ color: #fff; text-shadow: 0 0 10px #fff; }}
            
            .hero {{ position: relative; height: 85vh; width: 100%; display: flex; align-items: center; }}
            .hero-bg {{ position: absolute; inset: 0; background: url('https://assets.nflxext.com/ffe/siteui/vlv3/f841d4c7-10e1-40af-bcae-07a3f8dc141a/f6d7434e-d6de-4185-a6d4-c77a2d08737b/US-en-20220502-popsignuptwoweeks-perspective_alpha_website_medium.jpg') center/cover; opacity: 0.5; }}
            .hero-grad {{ position: absolute; inset: 0; background: radial-gradient(circle at center, transparent 0%, #0f0f0f 110%), linear-gradient(to top, #0f0f0f 5%, transparent 60%); }}
            .hero-content {{ position: relative; z-index: 10; padding-left: 5%; max-width: 700px; margin-top: 40px; }}
            .highlight-badge {{ background: rgba(229,9,20,0.8); padding: 5px 10px; font-size: 0.8rem; font-weight: 700; border-radius: 20px; text-transform: uppercase; letter-spacing: 1px; }}
            .title {{ font-size: 5rem; line-height: 0.9; margin: 10px 0; font-weight: 900; text-shadow: 0 10px 30px rgba(0,0,0,0.8); }}
            .desc {{ font-size: 1.2rem; color: #ddd; max-width: 500px; margin-bottom: 30px; }}
            
            .btns {{ display: flex; gap: 15px; }}
            .btn {{ padding: 12px 35px; border-radius: 50px; text-decoration: none; font-weight: 700; display: flex; align-items: center; gap: 8px; transition: transform 0.2s; }}
            .play {{ background: #fff; color: #000; }}
            .play:hover {{ transform: scale(1.05); box-shadow: 0 0 20px rgba(255,255,255,0.4); }}
            .glass {{ background: rgba(100,100,100,0.4); backdrop-filter: blur(10px); color: #fff; border: 1px solid rgba(255,255,255,0.2); }}
            
            .main-container {{ position: relative; z-index: 20; margin-top: -80px; padding-bottom: 50px; }}
            .section {{ margin-bottom: 40px; padding-left: 5%; }}
            .section h3 {{ font-size: 1.5rem; margin-bottom: 15px; font-weight: 600; color: #e5e5e5; border-left: 4px solid var(--primary); padding-left: 10px; }}
            
            .scroll-row {{ display: flex; gap: 20px; overflow-x: auto; padding: 20px 0; padding-right: 5%; scroll-behavior: smooth; }}
            .scroll-row::-webkit-scrollbar {{ display: none; }}
            
            .glass-card {{ flex: 0 0 260px; height: 160px; position: relative; border-radius: 12px; overflow: hidden; transition: 0.4s; cursor: pointer; background: #1a1a1a; border: 1px solid rgba(255,255,255,0.05); }}
            .card-bg {{ position: absolute; inset: 0; transition: 0.4s; opacity: 0.7; }}
            .Movies {{ background: linear-gradient(45deg, #4a0e12, #1a0506); }}
            .Series {{ background: linear-gradient(45deg, #0f2b4a, #030812); }}
            .Games {{ background: linear-gradient(45deg, #0e3b12, #020f04); }}
            
            .glass-card:hover {{ transform: scale(1.15); z-index: 100; box-shadow: 0 15px 40px rgba(0,0,0,0.8); border-color: rgba(255,255,255,0.3); }}
            .glass-card:hover .card-bg {{ opacity: 0.4; }}
            
            .card-content {{ position: absolute; inset: 0; padding: 15px; display: flex; flex-direction: column; justify-content: flex-end; }}
            .card-top {{ position: absolute; top: 10px; left: 15px; right: 15px; display: flex; justify-content: space-between; opacity: 0; transition: 0.3s; }}
            .glass-card:hover .card-top {{ opacity: 1; }}
            
            .card-info h4 {{ margin: 0; font-size: 1.1rem; text-shadow: 0 2px 4px rgba(0,0,0,1); transition: 0.3s; transform-origin: left; }}
            .glass-card:hover .card-info h4 {{ transform: translateY(-5px) scale(0.9); }}
            
            .meta-row {{ display: flex; gap: 10px; font-size: 0.8rem; color: #bbb; margin-top: 5px; }}
            .match {{ color: #46d369; font-weight: 700; }}
            
            .hover-details {{ max-height: 0; opacity: 0; overflow: hidden; transition: 0.4s; font-size: 0.8rem; color: #ddd; margin-top: 5px; }}
            .glass-card:hover .hover-details {{ max-height: 200px; opacity: 1; }}
            
            .actions {{ display: flex; gap: 10px; margin-top: 10px; }}
            .play-btn {{ flex: 1; border: none; background: #fff; color: #000; border-radius: 4px; font-weight: 700; cursor: pointer; padding: 5px; }}
            .icon-btn {{ width: 30px; height: 30px; border-radius: 50%; border: 1px solid #fff; display: flex; align-items: center; justify-content: center; color: #fff; text-decoration: none; }}
            .icon-btn:hover {{ background: rgba(255,255,255,0.2); }}
            
            .badge {{ background: rgba(255,255,255,0.2); backdrop-filter: blur(5px); padding: 2px 8px; border-radius: 4px; font-size: 0.7rem; }}
            .hd {{ border: 1px solid #aaa; padding: 0 4px; border-radius: 2px; font-size: 0.7rem; }}
            .quick-review input {{ background: rgba(255,255,255,0.1); border:none; padding:8px; width:100%; border-radius:4px; color:white; margin-top:10px; font-size:0.7rem; }}

            .search-header {{ padding: 120px 5% 40px; text-align: center; }}
            .glass-box {{ background: var(--glass); backdrop-filter: blur(20px); border: var(--glass-border); padding: 40px; border-radius: 20px; display: inline-block; width: 100%; max-width: 700px; box-sizing: border-box; }}
            .search-bar {{ display: flex; gap: 10px; margin-top: 20px; flex-wrap: wrap; }}
            .search-bar input {{ flex: 1; padding: 15px; background: rgba(0,0,0,0.3); border: 1px solid #444; color: #fff; border-radius: 8px; font-size: 1rem; min-width: 200px; }}
            .search-bar input.sm {{ flex: 0 0 100px; }}
            .search-bar button {{ padding: 0 30px; background: var(--primary); color: #fff; border: none; border-radius: 8px; font-weight: 700; cursor: pointer; }}
            .grid-container {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 30px; padding: 0 5%; }}
            .grid-container .glass-card {{ width: 100%; }}
            
            .dash-container {{ padding: 120px 5%; }}
            .stats-grid {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; }}
            .stat-glass {{ background: rgba(30,30,30,0.6); backdrop-filter: blur(10px); padding: 30px; border-radius: 15px; border: var(--glass-border); text-align: center; transition: 0.3s; }}
            .stat-glass:hover {{ transform: translateY(-5px); }}
            .stat-glass h3 {{ font-size: 2.5rem; margin: 10px 0; color: var(--primary); }}
            .gold {{ color: #ffd700 !important; }}
            .wide {{ grid-column: span 2; }}
            @media (max-width: 768px) {{ .title {{ font-size: 3rem; }} .hero {{ height: 70vh; }} .glass-card {{ flex: 0 0 200px; height: 130px; }} .grid-container {{ grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); }} .nav-links a {{ display: none; }} .wide {{ grid-column: span 1; }} }}
        </style></head><body>
        <div class='nav'>
            <a href='/' class='brand'>STREAM ATLAS</a>
            <div class='nav-links'>
                <a href='/'>Home</a><a href='/search'>Search</a><a href='/dashboard'>Analytics</a>
                {(u != null ? $"<a href='/logout' style='color:var(--primary)'>Logout ({u.Username})</a>" : "<form action='/login' method='post' style='display:inline;margin-left:20px'><input name='u' placeholder='Login ID' style='padding:5px;border-radius:4px;border:none;background:#333;color:#fff'><button style='display:none'></button></form>")}
            </div>
        </div>
        {c}</body></html>";

        User GetUser(HttpListenerRequest r) { var c = r.Cookies["sid"]; return (c != null && _sessions.ContainsKey(c.Value)) ? _sessions[c.Value] : null; }

        Dictionary<string, string> Parse(HttpListenerRequest r)
        {
            using (var s = new StreamReader(r.InputStream))
            {
                var d = new Dictionary<string, string>(); string b = s.ReadToEnd();
                if (b.Length > 0) foreach (var p in b.Split('&')) { var k = p.Split('='); if (k.Length == 2) d[k[0]] = WebUtility.UrlDecode(k[1]); }
                foreach (var k in new[] { "type", "t", "d", "y", "e1", "e2", "genres", "u", "txt", "rating", "id" }) if (!d.ContainsKey(k)) d[k] = ""; return d;
            }
        }
    }
}