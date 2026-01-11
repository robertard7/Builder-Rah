rah session list
$session = (rah session start | ConvertFrom-Json).sessionId
rah session send --id $session --message "Summarize the attached document"
rah session status --id $session
rah session plan --id $session
rah session cancel --id $session
