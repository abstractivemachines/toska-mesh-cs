{{/*
Expand the name of the chart.
*/}}
{{- define "toskamesh.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "toskamesh.fullname" -}}
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
{{- define "toskamesh.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "toskamesh.labels" -}}
helm.sh/chart: {{ include "toskamesh.chart" . }}
{{ include "toskamesh.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "toskamesh.selectorLabels" -}}
app.kubernetes.io/name: {{ include "toskamesh.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "toskamesh.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "toskamesh.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
External service addresses
*/}}
{{- define "toskamesh.consul.address" -}}
{{- .Values.externalServices.consul.address }}
{{- end }}

{{- define "toskamesh.postgres.host" -}}
{{- .Values.externalServices.postgres.host }}
{{- end }}

{{- define "toskamesh.rabbitmq.host" -}}
{{- .Values.externalServices.rabbitmq.host }}
{{- end }}
