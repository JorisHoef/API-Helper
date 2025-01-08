using Newtonsoft.Json;

namespace JorisHoef.APIHelper.Example.Models
{
    public class PostsData
    {
        [JsonProperty("userId")] public int UserId { get; set; }
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("body")] public string Body { get; set; }

        public PostsData() { } //Empty json constructor

        public PostsData(int userId, string title, string body)
        {
            this.UserId = userId;
            this.Title = title;
            this.Body = body;
        }

        public PostsData(int id, int userId, string title, string body)
        {
            this.Id = id;
            this.UserId = userId;
            this.Title = title;
            this.Body = body;
        }

        public static PostsData CreateFakePostsData(int userId, string title, string body)
        {
            return new PostsData(userId, title, body);
        }

        public static PostsData UpdateFakePostsData(int id, int userId, string title, string body)
        {
            return new PostsData(id, userId, title, body);
        }
    }
}