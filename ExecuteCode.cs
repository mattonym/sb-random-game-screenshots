using System;
using System.Net;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

public class CPHInline
{
    public bool Execute()
    {
    	
    	// may need a slightly nicer way to have defaults, empty values wil fail
    	// how you set these up is up to you, but i prefer to have the secret and client set up in their own subaction that executes
    	// at the top of the action that calls this.  they can be reused for any twitch api action.
        int ratingLower = args.ContainsKey("ratingLowerBound") ? int.Parse(args["ratingLowerBound"].ToString().Trim()) : 50;
        string client = args.ContainsKey("credClient") ? args["credClient"].ToString().Trim() : string.Empty;
        string secret = args.ContainsKey("credSecret") ? args["credSecret"].ToString().Trim() : string.Empty;
        string credApi = args.ContainsKey("credApi") ? args["credApi"].ToString().Trim() : string.Empty;
        string filePath = args.ContainsKey("screenshotPath") ? args["screenshotPath"].ToString().Trim() : string.Empty;
        
        // at the moment these are hardcoded
        string gamesApi = "https://api.igdb.com/v4/games";
        string characterApi = "https://api.igdb.com/v4/characters";
        string mugApi = "https://api.igdb.com/v4/character_mug_shots";

        // get credentials from twitch
        credApi = String.Format(credApi, client, secret);

		// at the moment this does not fail gracefully. there are a lot of assumptions based on the above args
        using (WebClient webClient = new WebClient{Encoding = System.Text.Encoding.UTF8})
        {
            string results = webClient.UploadString(credApi, string.Empty);
            JObject tokenObj = JObject.Parse(results);
            string access_token = tokenObj["access_token"].ToString();
            string game_body = "fields name, summary, platforms, platforms.name, platforms.platform_family, genres, genres.name, cover.url, first_release_date, screenshots, screenshots.height, screenshots.image_id, screenshots.url, screenshots.width; where first_release_date != null & rating > {0} & platforms != (34,39,74,82) & themes != (42) & version_parent = null & genres != (32); limit 300;";
            game_body = String.Format(game_body, ratingLower);
            webClient.Headers.Add("Client-Id", client);
            webClient.Headers.Add("Authorization", "Bearer " + access_token);
            results = webClient.UploadString(gamesApi, game_body);
            List<Game> listOfGames = JsonConvert.DeserializeObject<List<Game>>(results);
            int ran = CPH.Between(0, listOfGames.Count - 1);
            Game game = listOfGames[ran];
            

            System.IO.DirectoryInfo di = new DirectoryInfo(filePath);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }

            int i = 0;
            foreach (Screenshot s in game.screenshots)
            {
                CPH.SetArgument("gameScreenshot" + i, s.finalUrl);
                using (WebClient wc = new WebClient{Encoding = System.Text.Encoding.UTF8})
                {
                    byte[] data = wc.DownloadData(s.finalUrl);
                    using (MemoryStream mem = new MemoryStream(data))
                    {
                        using (var yourImage = Image.FromStream(mem))
                        {
                            yourImage.Save(filePath + i + ".jpg", ImageFormat.Jpeg);
                        }
                    }
                }
                
                CPH.SetArgument("gameScreenshot" + i + "height", "720");
                CPH.SetArgument("gameScreenshot" + i + "width", "1280");
                
                
                i += 1;
            }
            
            // set any new arguments here.  i have these fairly bare bones, but review the class below to figure out what you may want.
            CPH.SetArgument("gameName", game.name);
            CPH.SetArgument("releaseDate", game.formattedDate);
            CPH.SetArgument("gameSummary", game.summary);
        }

        return true;
    }

    public DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }
}

public class Game
{
    public int id { get; set; }

    public long first_release_date { get; set; }

    public string name { get; set; }

    public string summary { get; set; }

    public Cover cover { get; set; }

    public List<Genre> genres { get; set; }

    public List<Platform> platforms { get; set; }

    public List<Screenshot> screenshots { get; set; }

    public string formattedDate
    {
        get
        {
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(this.first_release_date).ToLocalTime();
            return dateTime.ToString("yyyy-MM-dd");
        }
    }
}

public class Cover
{
    public int id { get; set; }

    public string name { get; set; }
}

public class Platform
{
    public string id { get; set; }

    public string name { get; set; }

    public string platform_family { get; set; }
}

public class Genre
{
    public int id { get; set; }

    public string name { get; set; }
}

public class Screenshot
{
    public int id { get; set; }

    public int height { get; set; }

    public string image_id { get; set; }

    public string url { get; set; }

    public int width { get; set; }

	// may need to break out the images into its own enum so that you could call up this on demand
    public readonly string image = "https://images.igdb.com/igdb/image/upload/t_{0}/{1}.jpg";
    public readonly string imageSize = "720p";
    public string finalUrl
    {
        get
        {
            return String.Format(image, imageSize, image_id);
        }
    }
}