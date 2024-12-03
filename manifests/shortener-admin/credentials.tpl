
# kubetpl:syntax:go-template

apiVersion: v1
kind: Secret
metadata:
  name: {{ .PREFIX }}-basic-auth
type: kubernetes.io/basic-auth
data:
  username: {{ .ADMIN_USER | b64enc | quote }}
  password: {{ .ADMIN_PASSWORD | b64enc | quote }}
---
apiVersion: v1
kind: Secret
metadata:
  name: {{ .PREFIX }}-postgresql
data:
  password: {{ .POSTGRES_PASSWORD | b64enc | quote }}
  postgres-password: {{ .POSTGRES_PASSWORD | b64enc | quote }}
  repmgr-password: {{ .REPMGR_PASSWORD | b64enc | quote }}
  admin-password: {{ .PGPOOL_PASSWORD | b64enc | quote }}
