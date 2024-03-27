using System.Threading.Tasks;
using Pulumi;
using Pulumi.Eks;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

class EksTest : Stack
{
    public EksTest()
    {
        // Create an EKS Cluster
        var cluster = new Cluster("my-cluster");

        KubeConfig = cluster.KubeconfigJson;

        // Create a Kubernetes provider instance using the cluster's kubeconfig
        var provider = new Pulumi.Kubernetes.Provider("k8s-provider", new Pulumi.Kubernetes.ProviderArgs
        {
            KubeConfig = cluster.KubeconfigJson
        });

        // Create a namespace for the nginx service
        var ns = new Pulumi.Kubernetes.Core.V1.Namespace("nginx-ns", new NamespaceArgs
        {
            Metadata = new ObjectMetaArgs()
            {
                Name = "nginx-ns"
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
        });

        // Deploy nginx using a Deployment resource
        var appLabels = new InputMap<string> { { "app", "nginx" } };
        var deployment = new Pulumi.Kubernetes.Apps.V1.Deployment("nginx-deployment", new DeploymentArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = ns.Metadata.Apply(n => n.Name),
                Name = "nginx",
            },
            Spec = new DeploymentSpecArgs
            {
                Selector = new LabelSelectorArgs
                {
                    MatchLabels = appLabels,
                },
                Replicas = 2,
                Template = new PodTemplateSpecArgs
                {
                    Metadata = new ObjectMetaArgs
                    {
                        Labels = appLabels,
                    },
                    Spec = new PodSpecArgs
                    {
                        Containers =
                        {
                            new ContainerArgs
                            {
                                Name = "nginx",
                                Image = "nginx:latest",
                            },
                        },
                    },
                },
            },
        }, new CustomResourceOptions
        {
            Provider = provider,
        });

        // Expose nginx using a LoadBalancer Service
        var service = new Pulumi.Kubernetes.Core.V1.Service("nginx-service", new ServiceArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = ns.Metadata.Apply(n => n.Name),
                Name = "nginx",
            },
            Spec = new ServiceSpecArgs
            {
                Selector = appLabels,
                Ports =
                {
                    new ServicePortArgs
                    {
                        Port = 80,
                        TargetPort = 80,
                    },
                },
                Type = "LoadBalancer",
            },
        }, new CustomResourceOptions
        {
            Provider = provider,
        });

        // Export the service's IP address
        this.NginxServiceIp = service.Status.Apply(s => s.LoadBalancer.Ingress[0].Ip);
    }

    [Output]
    public Output<string> NginxServiceIp { get; set; }

    [Output]
    public Output<string> KubeConfig { get; set; }
}

class Program
{
    static Task<int> Main(string[] args) => Deployment.RunAsync<EksTest>();
}