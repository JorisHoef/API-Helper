namespace JorisHoef.API.Services.Base
{
    public class PostApiService<TResponse> : ApiService<TResponse>
    {
        protected override HttpMethod HttpMethod => HttpMethod.POST;
    }
}