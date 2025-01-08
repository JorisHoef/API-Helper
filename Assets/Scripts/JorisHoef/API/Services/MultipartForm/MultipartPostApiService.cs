namespace JorisHoef.API.Services.MultipartForms
{
    public class MultipartPostApiService<TResponse> : MultipartApiService<TResponse>
    {
        protected override HttpMethod HttpMethod => HttpMethod.POST;
    }
}