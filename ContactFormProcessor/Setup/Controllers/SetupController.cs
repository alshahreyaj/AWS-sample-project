using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.Runtime;
using Amazon.SimpleNotificationService.Model;
using Amazon.SimpleNotificationService;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.APIGateway;
using Amazon.APIGateway.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;

namespace Setup.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class SetupController : ControllerBase
  {
    BasicAWSCredentials credentials = new BasicAWSCredentials("test", "test");

    [HttpPost]
    [Route("CreateAndDeployAPI")]
    public async void CreateAndDeployAPI()
    {
      var apiConfig = new AmazonAPIGatewayConfig
      {
        ServiceURL = "http://localhost:4566",
        AuthenticationRegion = "eu-west-1"
      };

      var apiGatewayClient = new AmazonAPIGatewayClient(credentials, apiConfig);

      var createApiResponse = await apiGatewayClient.CreateRestApiAsync(new CreateRestApiRequest
      {
        Name = "MyApi",
        Description = "My API Gateway"
      });

      string apiId = createApiResponse.Id;

      var createResourceResponse = await apiGatewayClient.CreateResourceAsync(new CreateResourceRequest
      {
        RestApiId = apiId,
        ParentId = (await apiGatewayClient.GetResourcesAsync(new GetResourcesRequest { RestApiId = apiId })).Items[0].Id,
        PathPart = "process-contact"
      });

      string resourceId = createResourceResponse.Id;

      await apiGatewayClient.PutMethodAsync(new PutMethodRequest
      {
        RestApiId = apiId,
        ResourceId = resourceId,
        HttpMethod = "POST",
        AuthorizationType = "NONE"
      });

      await apiGatewayClient.PutIntegrationAsync(new PutIntegrationRequest
      {
        RestApiId = apiId,
        ResourceId = resourceId,
        HttpMethod = "POST",
        Type = "AWS_PROXY", // Integration with Lambda
        IntegrationHttpMethod = "POST",
        Uri = "arn:aws:apigateway:eu-west-1:lambda:path/2015-03-31/functions/arn:aws:lambda:eu-west-1:000000000000:function:processor/invocations"
      });

      var createReaderResourceResponse = await apiGatewayClient.CreateResourceAsync(new CreateResourceRequest
      {
        RestApiId = apiId,
        ParentId = (await apiGatewayClient.GetResourcesAsync(new GetResourcesRequest { RestApiId = apiId })).Items[0].Id,
        PathPart = "get-contacts"
      });

      string readerResourceId = createReaderResourceResponse.Id;

      await apiGatewayClient.PutMethodAsync(new PutMethodRequest
      {
        RestApiId = apiId,
        ResourceId = readerResourceId,
        HttpMethod = "GET",
        AuthorizationType = "NONE"
      });

      await apiGatewayClient.PutIntegrationAsync(new PutIntegrationRequest
      {
        RestApiId = apiId,
        ResourceId = readerResourceId,
        HttpMethod = "GET",
        Type = "AWS_PROXY", // Integration with Lambda
        IntegrationHttpMethod = "POST",
        Uri = "arn:aws:apigateway:eu-west-1:lambda:path/2015-03-31/functions/arn:aws:lambda:eu-west-1:000000000000:function:reader/invocations"
      });

      //await apiGatewayClient.PutMethodResponseAsync(new PutMethodResponseRequest
      //{
      //  RestApiId = apiId,
      //  ResourceId = resourceId,
      //  HttpMethod = "POST",
      //  StatusCode = "200",
      //  ResponseModels = { ["application/json"] = "Empty" }, // Response models if applicable
      //  ResponseParameters = { ["method.response.header.Content-Type"] = true } // Response headers if applicable
      //});

      //await apiGatewayClient.PutIntegrationResponseAsync(new PutIntegrationResponseRequest
      //{
      //  RestApiId = apiId,
      //  ResourceId = resourceId,
      //  HttpMethod = "POST",
      //  StatusCode = "200",
      //  SelectionPattern = "", // Optional selection pattern
      //  ResponseTemplates = { ["application/json"] = "" }
      //});

      var createDeploymentResponse = await apiGatewayClient.CreateDeploymentAsync(new CreateDeploymentRequest
      {
        RestApiId = apiId,
        StageName = "prod"
      });

      //string apiUrl = $"https://{apiId}.execute-api.{Amazon.RegionEndpoint.USWest2.SystemName}.amazonaws.com/prod";
      //Console.WriteLine("API URL: " + apiUrl);
    }


    [HttpPost]
    [Route("CrateLambdas")]
    public async void CrateLambdas()
    {
      var lambdaClient = new AmazonLambdaClient(new AmazonLambdaConfig
      {
        ServiceURL = "http://localhost:4566",
        AuthenticationRegion = "eu-west-1"
      });

      var createFunctionRequest = new CreateFunctionRequest
      {
        FunctionName = "processor",
        Role = "arn:aws:iam::000000000000:role/lmabda-role",
        Handler = "ContactFormProcessor::ContactFormProcessor.Function::FunctionHandler",
        Runtime = "dotnet6",
        Code = new FunctionCode
        {
          ZipFile = new MemoryStream(System.IO.File.ReadAllBytes("D:\\LocalStack\\assignment\\ContactFormProcessor\\ContactFormProcessor\\ContactFormProcessor.zip")) // Path to your published .zip file
        }
      };
      var createFunctionResponse = await lambdaClient.CreateFunctionAsync(createFunctionRequest);

      var createReaderFunctionRequest = new CreateFunctionRequest
      {
        FunctionName = "reader",
        Role = "arn:aws:iam::000000000000:role/lmabda-role",
        Handler = "ContactFormReader::ContactFormReader.Function::FunctionHandler",
        Runtime = "dotnet6",
        Code = new FunctionCode
        {
          ZipFile = new MemoryStream(System.IO.File.ReadAllBytes("D:\\LocalStack\\assignment\\ContactFormProcessor\\ContactFormReader\\ContactFormReader.zip")) // Path to your published .zip file
        }
      };
      await lambdaClient.CreateFunctionAsync(createReaderFunctionRequest);
    }


    [HttpPost]
    [Route("SetupProject")]
    public async void SetupProject()
    {
      #region Create Dynamodb Table
      var dynamoDbConfig = new AmazonDynamoDBConfig
      {
        ServiceURL = "http://localhost:4566",
        AuthenticationRegion = "eu-west-1"
      };
      var clientDB = new AmazonDynamoDBClient(credentials, dynamoDbConfig);

      var requestDB = new CreateTableRequest
      {
        TableName = "contacts",
        KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement { AttributeName = "email", KeyType = KeyType.HASH }
            },
        AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition { AttributeName = "email", AttributeType = ScalarAttributeType.S }
            },
        BillingMode = BillingMode.PAY_PER_REQUEST
      };

      var responseDB = await clientDB.CreateTableAsync(requestDB);
      #endregion

      #region Create SQS
      var sqsConfig = new AmazonSQSConfig
      {
        ServiceURL = "http://localhost:4566",
        AuthenticationRegion = "eu-west-1"
      };
      var sqsClient = new AmazonSQSClient(credentials, sqsConfig);
      await CreateQueue(sqsClient, "ContactFormQueue");
      #endregion

      #region Create SNS
      var snsConfig = new AmazonSimpleNotificationServiceConfig
      {
        ServiceURL = "http://localhost:4566",
        AuthenticationRegion = "eu-west-1"
      };
      IAmazonSimpleNotificationService snsClient = new AmazonSimpleNotificationServiceClient(credentials, snsConfig);

      var topicArn = await CreateSNSTopicAsync(snsClient, "ContactFormTopic");
      #endregion

      #region Subscribe to SNS
      var subscribeRequest = new SubscribeRequest
      {
        Protocol = "email",
        Endpoint = "testemail@gmail.com",
        TopicArn = "arn:aws:sns:eu-west-1:000000000000:ContactFormTopic"
      };

      var subscribeResponse = await snsClient.SubscribeAsync(subscribeRequest);
      #endregion

      #region Create s3 bucket
      var s3Config = new AmazonS3Config
      {
        ServiceURL = "http://s3.localhost.localstack.cloud:4566",
        AuthenticationRegion = "eu-west-1",
      };
      var s3Client = new AmazonS3Client(credentials, s3Config);

      var putBucketRequest = new PutBucketRequest
      {
        BucketName = "images",
        BucketRegion = "us-west-2"
      };

      try
      {
        var response = await s3Client.PutBucketAsync(putBucketRequest);
      }
      catch (AmazonS3Exception ex)
      {
        Console.WriteLine($"Error creating bucket: {ex.Message}");
      }
      #endregion

    }

    private static async Task<string> CreateQueue(
      IAmazonSQS sqsClient, string qName, string deadLetterQueueUrl = null,
      string maxReceiveCount = null, string receiveWaitTime = null)
    {
      var attrs = new Dictionary<string, string>();

      // Create the queue
      CreateQueueResponse responseCreate = await sqsClient.CreateQueueAsync(
          new CreateQueueRequest { QueueName = qName, Attributes = attrs });
      return responseCreate.QueueUrl;
    }

    public static async Task<string> CreateSNSTopicAsync(IAmazonSimpleNotificationService client, string topicName)
    {
      var request = new CreateTopicRequest
      {
        Name = topicName,
      };

      var response = await client.CreateTopicAsync(request);

      return response.TopicArn;
    }
  }
}
