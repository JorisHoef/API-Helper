using Newtonsoft.Json;

namespace JorisHoef.Example.Models
{
    public class PostData
    {
        [JsonProperty("userId")] public int UserId { get; set; }
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("body")] public string Body { get; set; }

        public PostData() { } //Empty json constructor

        public PostData(int userId, string title, string body)
        {
            this.UserId = userId;
            this.Title = title;
            this.Body = body;
        }

        public PostData(int id, int userId, string title, string body)
        {
            this.Id = id;
            this.UserId = userId;
            this.Title = title;
            this.Body = body;
        }

        public static PostData CreateFakePostsData(int userId, string title, string body)
        {
            return new PostData(userId, title, body);
        }

        public static PostData UpdateFakePostsData(int id, int userId, string title, string body)
        {
            return new PostData(id, userId, title, body);
        }
    }
}