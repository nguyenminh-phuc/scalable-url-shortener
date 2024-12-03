{{/*
Expand the name of the chart.
*/}}
{{- define "shortener-frontend.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "shortener-frontend.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "shortener-frontend.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "shortener-frontend.labels" -}}
helm.sh/chart: {{ include "shortener-frontend.chart" . }}
{{ include "shortener-frontend.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{- define "shortener-frontend.restLabels" -}}
{{ include "shortener-frontend.labels" . }}
shortener/type: rest-frontend
{{- end }}

{{- define "shortener-frontend.graphqlLabels" -}}
{{ include "shortener-frontend.labels" . }}
shortener/type: graphql-frontend
{{- end }}

{{- define "shortener-frontend.redirectLabels" -}}
{{ include "shortener-frontend.labels" . }}
shortener/type: redirect-frontend
{{- end }}

{{/*
Selector labels
*/}}
{{- define "shortener-frontend.selectorLabels" -}}
app.kubernetes.io/name: {{ include "shortener-frontend.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{- define "shortener-frontend.selectorRestLabels" -}}
{{ include "shortener-frontend.selectorLabels" . }}
shortener/type: rest-frontend
{{- end }}

{{- define "shortener-frontend.selectorGraphqlLabels" -}}
{{ include "shortener-frontend.selectorLabels" . }}
shortener/type: graphql-frontend
{{- end }}

{{- define "shortener-frontend.selectorRedirectLabels" -}}
{{ include "shortener-frontend.selectorLabels" . }}
shortener/type: redirect-frontend
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "shortener-frontend.serviceAccountName" -}}
{{- include "shortener-frontend.fullname" . }}
{{- end }}

{{- define "shortener-frontend.checkSecret" -}}
{{- $name := .name }}
{{- $namespace := .namespace }}
echo "Checking {{ $name }} secret..."
if ! kubectl get secret {{ $name }} -n {{ $namespace }}; then
  echo "Secret {{ $name }} not found."
  exit 1
fi
{{- end }}

{{- define "shortener-frontend.getReplicasAndCheckReady" -}}
{{- $name := .name }}
{{- $type := .type }}
{{- $namespace := .namespace }}
echo "Getting {{ $type }}/{{ $name }} replicas..."
REPLICAS=$(kubectl get -o=jsonpath='{.status.replicas}' {{ $type }}/{{ $name }} -n {{ $namespace }})
echo "Checking {{ $type }}/{{ $name }}..."
kubectl wait --for=jsonpath='{.status.readyReplicas}'=$REPLICAS {{ $type }}/{{ $name }} -n {{ $namespace }} --timeout=5m
if [ $? -ne 0 ]; then
  exit 1
fi
{{- end }}

{{- define "shortener-frontend.waitForDependencies" -}}
- name: wait-for-dependencies
  image: {{ .Values.frontend.jobs.kubectl }}
  command: [ "/bin/sh", "-c" ]
{{- if .Values.frontend.jobs.useIstio }}
  securityContext:
    runAsUser: 1337
{{- end }}
  args:
    - |
{{ include "shortener-frontend.getReplicasAndCheckReady" (dict
  "name" .Values.zookeeper.statefulSetName
  "type" "statefulset"
  "namespace" .Release.Namespace) | indent 6 }}
{{ include "shortener-frontend.getReplicasAndCheckReady" (dict
  "name" .Values.redis.statefulSetName
  "type" "statefulset"
  "namespace" .Release.Namespace) | indent 6 }}
      echo "Done."
{{- end }}

{{- define "shortener-frontend.useIstio" -}}
{{- if .Values.frontend.jobs.useIstio }}
trap "curl --max-time 2 -s -f -XPOST http://127.0.0.1:15020/quitquitquit" EXIT
while ! curl -s -f http://127.0.0.1:15020/healthz/ready; do sleep 1; done
echo "Ready!"
{{- end }}
{{- end }}
