## Misfits Add: Examine flavor text for Critical/SoftCritical mob states.

# Full Critical — completely incapacitated, urgent.
misfits-crit-examine-critical = [color=red]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } blue in the face and barely breathing — they need medical attention immediately![/color]

# SoftCritical — conscious but badly hurt, still needs help.
misfits-crit-examine-softcritical = [color=orange]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BE($target) } badly hurt and struggling to stay on { POSS-ADJ($target) } feet — { SUBJECT($target) } could use some healing.[/color]
