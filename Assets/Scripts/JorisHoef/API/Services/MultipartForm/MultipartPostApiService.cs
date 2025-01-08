namespace JorisHoef.API.Services.MultipartForm
{
    public class MultipartPostApiService<TResponse> : MultipartApiService<TResponse>
    {
        protected override HttpMethod HttpMethod => HttpMethod.POST;
    }
}