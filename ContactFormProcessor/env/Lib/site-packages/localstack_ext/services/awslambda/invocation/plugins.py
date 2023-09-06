from localstack.services.awslambda.invocation.plugins import RuntimeExecutorPlugin
class KubernetesRuntimeExecutorPlugin(RuntimeExecutorPlugin):
	name='kubernetes'
	def load(B,*C,**D):from localstack_ext.services.awslambda.invocation.kubernetes_runtime_executor import KubernetesRuntimeExecutor as A;return A