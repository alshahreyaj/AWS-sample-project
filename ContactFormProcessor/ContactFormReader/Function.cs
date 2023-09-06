using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Runtime;
using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.S3.Model;
using System.Linq;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ContactFormReader
{
  public class Function
  {
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
      var credentials = new BasicAWSCredentials("test", "test");
      var _serviceURL = $"http://{System.Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME")}:{System.Environment.GetEnvironmentVariable("EDGE_PORT")}";

      var configDB = new AmazonDynamoDBConfig
      {
        ServiceURL = _serviceURL,
        AuthenticationRegion = "eu-west-1"
      };
      var client = new AmazonDynamoDBClient(credentials, configDB);

      // Parse query parameters for pagination
      int pageSize = GetQueryParameterInt(request, "pageSize", 10);
      int pageNum = GetQueryParameterInt(request, "pageNum", 1);
      int startIndex = (pageNum - 1) * pageSize;

      var scanRequest = new ScanRequest
      {
        TableName = "contacts"
      };

      var scanResponse = await client.ScanAsync(scanRequest);

      var items = scanResponse.Items.Skip(startIndex).Take(pageSize);
      var formattedData = new List<Dictionary<string, string>>();

      var s3Config = new AmazonS3Config
      {
        ServiceURL = _serviceURL,
        AuthenticationRegion = "eu-west-1",
      };
      var s3Client = new AmazonS3Client(credentials, s3Config);

      foreach (var item in items)
      {
        var formattedItem = new Dictionary<string, string>();

        foreach (var attribute in item)
        {
          // Extract the value if it's not null or empty
          if (!string.IsNullOrEmpty(attribute.Value.S))
          {
            if (attribute.Key == "imageID")
            {
              GetPreSignedUrlRequest requestDB = new GetPreSignedUrlRequest
              {
                BucketName = "images",
                Key = attribute.Value.S,
                Expires = DateTime.Now.AddHours(6) // URL expiration time
              };

              string preSignedUrl = s3Client.GetPreSignedURL(requestDB);
              string modifiedUrl = preSignedUrl.Replace("172.18.0.2", "localhost");
              formattedItem["imageURL"] = modifiedUrl;
              continue;
            }
            formattedItem[attribute.Key] = attribute.Value.S;
          }
        }

        formattedData.Add(formattedItem);
      }

      return new APIGatewayProxyResponse
      {
        StatusCode = 200,
        Body = Newtonsoft.Json.JsonConvert.SerializeObject(formattedData),
        Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                }
      };
    }

    private int GetQueryParameterInt(APIGatewayProxyRequest request, string paramName, int defaultValue)
    {
      if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(paramName))
      {
        if (int.TryParse(request.QueryStringParameters[paramName], out int parsedValue))
        {
          return parsedValue;
        }
      }
      return defaultValue;
    }
  }
}
