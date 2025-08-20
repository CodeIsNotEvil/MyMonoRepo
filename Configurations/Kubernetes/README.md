# Kubernetes

Cluster management and orchastration Tool

### Configuration

configuration file lives at `~/.kube/config`

## Kubectl

**Commandline** interface for **Kubernetes** and **Minikube**

### Installation

Check the guide in the official [docs](https://kubernetes.io/docs/tasks/tools/install-kubectl-linux/#install-using-native-package-management)

In Pop!_OS 22.04 it is done by adding the kubernetes Repository to apt and installing it:

The version of Kubernetes and Kubectl must match.

```bash
sudo apt install -y apt-transport-https ca-certificates curl gnupg

curl -fsSL https://pkgs.k8s.io/core:/stable:/v1.33/deb/Release.key | sudo gpg --dearmor -o /etc/apt/keyrings/kubernetes-apt-keyring.gpg

sudo chmod 644 /etc/apt/keyrings/kubernetes-apt-keyring.gpg

echo 'deb [signed-by=/etc/apt/keyrings/kubernetes-apt-keyring.gpg] https://pkgs.k8s.io/core:/stable:/v1.33/deb/ /' | sudo tee /etc/apt/sources.list.d/kubernetes.list

sudo chmod 644 /etc/apt/sources.list.d/kubernetes.list

sudo apt update

sudo apt install -y kubectl
```

#### Recomaned after setup

Alias `Kubectl` to `k` (nushell: `alias k = kubectl`)

### Common command usage

- #### Cluster
    - `k cluster-info` basic cluster Information (Contorl plane address & DNS endpoint)
- #### Node
    - `k get nodes` list of Nodes(maschines) in the Cluster
    - `k describe node minikube` list everying about the **node** minikube
- #### namespace
    - `k get namespace` shows the namespaces, default is used if non are defined by the administrator
    - `k create namespace dev` creates a new namespace with the name of dev
    - `k create -f namespace-prod.yaml` create everythig specified inside the yaml file `namespace-prod.yaml` 
    - `k delete namespace prod` deletes the namespace named prod
    - `k describe namespace prod` list everyting about the **namespace** prod
    - `k config set-context --current --namespace=dev` Switching the context to the dev namepace
- #### pods
    - `k get pods` show pods in the default namespaces
    - `k get pods -n dev` only shows the pods inside the dev namespace
    - `k get pods --all-namespaces` shows all pods in all namespaces
- #### deployments
    - `k create deployment hello-node --image=k8s.gcr.io/echoserver:1.4` create the hellow world depolyment inside the **default** namespace
    - `k create deployment hello-node --image=k8s.gcr.io/echoserver:1.4 -n dev` create the hellow world depolyment inside the **dev** namespace

- #### events
    - `k get events` shows the evntes in the default namespace
    - `k get events -n dev` shows the events in the dev namespace
    - `k get events --all-namespaces` shows events in all namespaces

## Minikube

Test enviorment!

Minikube is a single maschine cluster. With the features of Kubernetes.

Made for testing, development and learning.

### Installation 

Check the guide in the official [docs](https://minikube.sigs.k8s.io/docs/start/?arch=%2Flinux%2Fx86-64%2Fstable%2Fdebian+package)

In Pop!_OS 22.04 it is done by installing the Debian package for the amd64(x86-64) platform

```bash
curl -LO https://storage.googleapis.com/minikube/releases/latest/minikube_latest_amd64.deb

sudo dpkg -i minikube_latest_amd64.deb

rm minikube_latest_amd64.deb
```

## Helm

Repository based Packagemanager for Kubernetes.

### Installation

Check the guide in the official [docs](https://helm.sh/docs/intro/install/)

In Pop!_OS 22.04 it is done by adding the baltocdn helm Repository to apt and installing it:

**Check if the arch matches** : `dpkg --print-architecture`

```bash
curl https://baltocdn.com/helm/signing.asc | gpg --dearmor | sudo tee /usr/share/keyrings/helm.gpg > /dev/null

sudo apt install apt-transport-https --yes

echo "deb [arch=amd64 signed-by=/usr/share/keyrings/helm.gpg] https://baltocdn.com/helm/stable/debian/ all main" | sudo tee /etc/apt/sources.list.d/helm-stable-debian.list

sudo apt update

sudo apt install helm
```
