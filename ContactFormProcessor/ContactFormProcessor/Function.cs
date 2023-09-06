using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;
using Amazon.DynamoDBv2;
using Amazon;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.SimpleNotificationService.Model;
using Amazon.SimpleNotificationService;
using static System.Net.WebRequestMethods;
using Amazon.S3.Model;
using Amazon.S3;
using HttpMultipartParser;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ContactFormProcessor;

public class Function
{

  public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
  {

    // Parse the incoming JSON payload
    //var requestBody = JsonConvert.DeserializeObject<RequestPayload>(request?.Body);

    // Extract values from the parsed object
    //var name = requestBody?.Name;
    //var email = requestBody?.Email;
    //var message = requestBody?.Message;
    //var imageData = Convert.FromBase64String(requestBody?.ImageData);

    var parser = HttpMultipartParser.MultipartFormDataParser.Parse(new MemoryStream(Convert.FromBase64String(request.Body)));

    var name = Convert.ToString(parser.GetParameterValue("name"));
    var email = Convert.ToString(parser.GetParameterValue("email"));
    var message = Convert.ToString(parser.GetParameterValue("message"));
    var imageID = "images/" + Guid.NewGuid() + ".jpg";

    var imageFile = parser.Files.First().Data;

    Console.WriteLine(name);
    Console.WriteLine(email);
    Console.WriteLine(message);
    Console.WriteLine(imageFile);
    var credentials = new BasicAWSCredentials("test", "test");
    var _serviceURL = $"http://{System.Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME")}:{System.Environment.GetEnvironmentVariable("EDGE_PORT")}";

    #region Put Item into DB
    var configDB = new AmazonDynamoDBConfig
    {
      ServiceURL = _serviceURL,
      AuthenticationRegion = "eu-west-1"
    };
    var client = new AmazonDynamoDBClient(credentials, configDB);
    var requestDB = new PutItemRequest
    {
      TableName = "contacts",
      Item = new Dictionary<string, AttributeValue>()
      {
          { "email", new AttributeValue { S = email }},
          { "name", new AttributeValue { S = name }},
          { "message", new AttributeValue { S = message }},
          { "imageID", new AttributeValue { S = imageID}}
      }
    };
    Console.WriteLine("test");
    try
    {
      await client.PutItemAsync(requestDB);

    }
    catch(Exception ex)
    {
      context.Logger.LogError(ex.Message);
      //Console.WriteLine(ex.Message);
    }
    #endregion

    Console.WriteLine("Inserted to db");

    #region Send notification to SQS
    var queueUrl = "http://127.0.0.1:4566/000000000000/ContactFormQueue";

    var sqsConfig = new AmazonSQSConfig
    {
      ServiceURL = _serviceURL,
      AuthenticationRegion = "eu-west-1"
    };
    var sqsClient = new AmazonSQSClient(credentials, sqsConfig);

    try
    {
      string messageBody = $"Received data: Name - {name}, Email - {email}, Message - {message}";

      var sendMessageRequest = new Amazon.SQS.Model.SendMessageRequest
      {
        QueueUrl = queueUrl,
        MessageBody = messageBody
      };
      var sendMessageResponse = await sqsClient.SendMessageAsync(sendMessageRequest);
    }
    catch (Exception ex)
    {
      Console.WriteLine("Error sending message: " + ex.Message);
    }
    #endregion

    #region Send Email
    var snsConfig = new AmazonSimpleNotificationServiceConfig
    {
      ServiceURL = _serviceURL,
      AuthenticationRegion = "eu-west-1"
    };
    IAmazonSimpleNotificationService snsClient = new AmazonSimpleNotificationServiceClient(credentials, snsConfig);

    var publishRequest = new PublishRequest
    {
      Message = $"Email sent to {name}",
      Subject = "Email",
      TopicArn = "arn:aws:sns:eu-west-1:000000000000:ContactFormTopic"
    };

    var publishResponse = await snsClient.PublishAsync(publishRequest);
    #endregion

    #region Upload image to s3
    var s3Config = new AmazonS3Config
    {
      ServiceURL = _serviceURL,
      AuthenticationRegion = "eu-west-1",
    };
    var s3Client = new AmazonS3Client(credentials, s3Config);
    var objectKey = imageID;


    // Upload the image to S3
    var putRequest = new PutObjectRequest
    {
      BucketName = "images",
      Key = objectKey,
      InputStream = imageFile
    };

    await s3Client.PutObjectAsync(putRequest);
    #endregion

    var responseMessage = $"Received data: Name - {name}, Email - {email}, Message - {message}";


    var response = new APIGatewayProxyResponse
    {
      StatusCode = 200,
      Body = responseMessage,
    };

    return response;
  }

  public class RequestPayload
  {
    public string Name { get; set; }
    public string Email { get; set; }
    public string Message { get; set; }
    public string ImageData { get; set; }
  }

}
