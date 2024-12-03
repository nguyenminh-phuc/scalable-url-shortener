
# kubetpl:syntax:go-template

apiVersion: v1
kind: Secret
metadata:
  name: {{ .PREFIX }}-acme
data:
  acmedns.json: {{ .ACME | b64enc | quote }}
