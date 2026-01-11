$session = (Invoke-RestMethod http://localhost:5050/sessions -Method Post).sessionId
Invoke-RestMethod "http://localhost:5050/sessions/$session/message" -Method Post -Body '{"text":"Summarize the attached document"}' -ContentType "application/json"
Invoke-RestMethod "http://localhost:5050/sessions/$session/status"
Invoke-RestMethod "http://localhost:5050/sessions/$session/cancel" -Method Post
Invoke-RestMethod "http://localhost:5050/provider/metrics"
