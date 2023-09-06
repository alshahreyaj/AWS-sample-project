#!/bin/bash
# unfortunately neither using JAVA_OPTS nor JAVA_TOOL_OPTIONS worked for this,
# so we have to use a wrapper script to set the property

# the path to the interpreter and all of the originally intended arguments
args=("$@")

# the extra options to pass to the interpreter
extra_args=("-Dcom.amazonaws.sdk.disableCertChecking")

# insert the extra options before the last argument
args=("${args[@]:0:$#-1}" "${extra_args[@]}" "${args[@]: -1}")

# start the runtime with the extra options
# this will look something like this:
#exec /var/lang/bin/java -XX:MaxHeapSize=734003k -XX:MaxMetaspaceSize=104858k -XX:ReservedCodeCacheSize=52429k -XX:+UseSerialGC -javaagent:/var/runtime/amzn-log4j-security-jdk8-0.1alpha.jar -Xshare:on -XX:-TieredCompilation -Djava.net.preferIPv4Stack=true -Dcom.amazonaws.sdk.disableCertChecking -classpath /var/runtime/lib/aws-lambda-java-core-1.2.2.jar:/var/runtime/lib/aws-lambda-java-runtime-0.2.0.jar:/var/runtime/lib/aws-lambda-java-serialization-0.2.0.jar lambdainternal.AWSLambda
exec "${args[@]}"
