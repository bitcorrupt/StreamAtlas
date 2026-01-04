using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace StreamAtlas_System
{
    public class StreamAtlasDL
    {
        // UPDATE THIS CONNECTION STRING for your SQL Server
        // Use "Data Source=.;" for local, or "Data Source=.\SQLEXPRESS;" if using Express
        private string _connStr = @"Data Source=.;Initial Catalog=StreamAtlasDB_v3;Integrated Security=True;";

        // --- CORE FETCH METHODS ---

        public List<MediaItem> GetHomeData(string uiType)
        {
            // Map UI plural types (Movies) to SQL singular types (Movie)
            string sqlType = uiType.TrimEnd('s');
            if (uiType == "Series") sqlType = "Series";

            string query = @"
                SELECT m.*, 
                       STRING_AGG(g.Name, ', ') WITHIN GROUP (ORDER BY g.Name) AS GenreList
                FROM v_AllMedia m
                LEFT JOIN Media_Genres mg ON m.Id = mg.MediaId AND m.Type = mg.MediaType
                LEFT JOIN Genres g ON mg.GenreId = g.GenreId
                WHERE m.Type = @Type
                GROUP BY m.Id, m.Title, m.Description, m.ReleaseYear, m.Rating, m.ExtraInfo, m.Creator, m.Type";

            return ExecuteFetch(query, new SqlParameter("@Type", sqlType));
        }

        public List<MediaItem> FilterMedia(string searchQuery, string genreFilter)
        {
            string sql = @"
                SELECT m.*, 
                       STRING_AGG(g.Name, ', ') WITHIN GROUP (ORDER BY g.Name) AS GenreList
                FROM v_AllMedia m
                LEFT JOIN Media_Genres mg ON m.Id = mg.MediaId AND m.Type = mg.MediaType
                LEFT JOIN Genres g ON mg.GenreId = g.GenreId
                WHERE 1=1 ";

            List<SqlParameter> p = new List<SqlParameter>();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                sql += @" AND (
                            m.Title LIKE @q 
                            OR m.Creator LIKE @q
                            OR EXISTS (
                                SELECT 1 FROM Media_Actors ma 
                                JOIN Actors a ON ma.ActorId = a.ActorId 
                                WHERE ma.MediaId = m.Id AND ma.MediaType = m.Type AND a.Name LIKE @q
                            )
                          )";
                p.Add(new SqlParameter("@q", "%" + searchQuery + "%"));
            }

            if (!string.IsNullOrEmpty(genreFilter))
            {
                sql += " GROUP BY m.Id, m.Title, m.Description, m.ReleaseYear, m.Rating, m.ExtraInfo, m.Creator, m.Type ";
                sql += " HAVING STRING_AGG(g.Name, ', ') LIKE @g";
                p.Add(new SqlParameter("@g", "%" + genreFilter + "%"));
            }
            else
            {
                sql += " GROUP BY m.Id, m.Title, m.Description, m.ReleaseYear, m.Rating, m.ExtraInfo, m.Creator, m.Type";
            }

            return ExecuteFetch(sql, p.ToArray());
        }

        // --- BUSINESS ACTIONS ---

        public User Login(string username)
        {
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();
                string check = "SELECT UserId, Username FROM Users WHERE Username = @u";
                using (SqlCommand cmd = new SqlCommand(check, conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read()) return GetUserWithWishlist(r["UserId"].ToString(), r["Username"].ToString());
                    }
                }

                string insert = "INSERT INTO Users (Username) OUTPUT INSERTED.UserId VALUES (@u)";
                using (SqlCommand cmd = new SqlCommand(insert, conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    int newId = (int)cmd.ExecuteScalar();
                    return new User { UserId = newId.ToString(), Username = username };
                }
            }
        }

        private User GetUserWithWishlist(string uid, string uname)
        {
            var u = new User { UserId = uid, Username = uname };
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();
                string sql = "SELECT MediaId FROM Wishlist WHERE UserId = @uid";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", uid);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read()) u.Wishlist.Add((int)r["MediaId"]);
                    }
                }
            }
            return u;
        }

        public void AddMedia(string type, string t, string d, int y, string e1, string e2, string genres)
        {
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();
                string sql = "";

                if (type == "Movies")
                {
                    int duration = int.TryParse(new string(e1.Where(char.IsDigit).ToArray()), out int val) ? val : 120;
                    sql = "INSERT INTO Movies (Title, Description, ReleaseYear, DurationMins, Director, Rating) VALUES (@t, @d, @y, @dur, @creat, 0)";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@t", t); cmd.Parameters.AddWithValue("@d", d);
                        cmd.Parameters.AddWithValue("@y", y); cmd.Parameters.AddWithValue("@dur", duration);
                        cmd.Parameters.AddWithValue("@creat", e2);
                        cmd.ExecuteNonQuery();
                    }
                }
                else if (type == "Series")
                {
                    int seasons = int.TryParse(new string(e1.Where(char.IsDigit).ToArray()), out int val) ? val : 1;
                    sql = "INSERT INTO Series (Title, Description, StartYear, Seasons, Network, EndYear) VALUES (@t, @d, @y, @sea, @creat, 0)";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@t", t); cmd.Parameters.AddWithValue("@d", d);
                        cmd.Parameters.AddWithValue("@y", y); cmd.Parameters.AddWithValue("@sea", seasons);
                        cmd.Parameters.AddWithValue("@creat", e2);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    sql = "INSERT INTO Games (Title, Description, ReleaseYear, Platform, Developer, Rating) VALUES (@t, @d, @y, @plat, @creat, 0)";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@t", t); cmd.Parameters.AddWithValue("@d", d);
                        cmd.Parameters.AddWithValue("@y", y); cmd.Parameters.AddWithValue("@plat", e1);
                        cmd.Parameters.AddWithValue("@creat", e2);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public void ToggleWishlist(string uid, int mid, string type)
        {
            string sqlType = type == "Movies" ? "Movie" : (type == "Games" ? "Game" : (type == "Series" ? "Series" : type));

            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();
                string check = "SELECT Count(*) FROM Wishlist WHERE UserId=@u AND MediaId=@m AND MediaType=@t";
                SqlCommand cmdCheck = new SqlCommand(check, conn);
                cmdCheck.Parameters.AddWithValue("@u", uid);
                cmdCheck.Parameters.AddWithValue("@m", mid);
                cmdCheck.Parameters.AddWithValue("@t", sqlType);

                int count = (int)cmdCheck.ExecuteScalar();

                if (count > 0)
                {
                    string del = "DELETE FROM Wishlist WHERE UserId=@u AND MediaId=@m AND MediaType=@t";
                    ExecuteNonQuery(del, conn, uid, mid, sqlType);
                }
                else
                {
                    string ins = "INSERT INTO Wishlist (UserId, MediaId, MediaType) VALUES (@u, @m, @t)";
                    ExecuteNonQuery(ins, conn, uid, mid, sqlType);
                }
            }
        }

        public void AddReview(string uid, int mid, string type, int rating, string txt)
        {
            string sqlType = type == "Movies" ? "Movie" : (type == "Games" ? "Game" : (type == "Series" ? "Series" : type));
            string sql = "INSERT INTO Reviews (UserId, MediaId, MediaType, Rating, Comment) VALUES (@u, @m, @t, @r, @txt)";

            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", uid); cmd.Parameters.AddWithValue("@m", mid);
                    cmd.Parameters.AddWithValue("@t", sqlType); cmd.Parameters.AddWithValue("@r", rating);
                    cmd.Parameters.AddWithValue("@txt", txt);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public (int Users, int Movies, int Series, int Games, string TopMovie) GetStats()
        {
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();
                int u = (int)new SqlCommand("SELECT COUNT(*) FROM Users", conn).ExecuteScalar();
                int m = (int)new SqlCommand("SELECT COUNT(*) FROM Movies", conn).ExecuteScalar();
                int s = (int)new SqlCommand("SELECT COUNT(*) FROM Series", conn).ExecuteScalar();
                int g = (int)new SqlCommand("SELECT COUNT(*) FROM Games", conn).ExecuteScalar();

                string topSql = @"SELECT TOP 1 m.Title FROM Reviews r
                                  JOIN v_AllMedia m ON r.MediaId = m.Id AND r.MediaType = m.Type
                                  GROUP BY m.Title ORDER BY COUNT(*) DESC";
                var topObj = new SqlCommand(topSql, conn).ExecuteScalar();
                string top = topObj != null ? topObj.ToString() : "None";

                return (u, m, s, g, top);
            }
        }

        private List<MediaItem> ExecuteFetch(string sql, params SqlParameter[] p)
        {
            var list = new List<MediaItem>();
            using (SqlConnection conn = new SqlConnection(_connStr))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    if (p != null) cmd.Parameters.AddRange(p);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new MediaItem
                            {
                                Id = (int)r["Id"],
                                Type = r["Type"].ToString(),
                                Title = r["Title"].ToString(),
                                Description = r["Description"].ToString(),
                                Year = Convert.ToInt32(r["ReleaseYear"]),
                                ExtraInfo = r["ExtraInfo"].ToString(),
                                Creator = r["Creator"].ToString(),
                                Genres = r["GenreList"] != DBNull.Value
                                         ? r["GenreList"].ToString().Split(new[] { ", " }, StringSplitOptions.None).ToList()
                                         : new List<string>()
                            });
                        }
                    }
                }
            }
            return list;
        }

        private void ExecuteNonQuery(string sql, SqlConnection conn, string uid, int mid, string type)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@u", uid);
                cmd.Parameters.AddWithValue("@m", mid);
                cmd.Parameters.AddWithValue("@t", type);
                cmd.ExecuteNonQuery();
            }
        }
    }

    // --- DATA MODELS ---
    public class MediaItem
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Year { get; set; }
        public string ExtraInfo { get; set; }
        public string Creator { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
    }

    public class User
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public List<int> Wishlist { get; set; } = new List<int>();
    }

    public class Review
    {
        public string UserId { get; set; }
        public int MediaId { get; set; }
        public string Type { get; set; }
        public int Rating { get; set; }
        public string Text { get; set; }
    }
}