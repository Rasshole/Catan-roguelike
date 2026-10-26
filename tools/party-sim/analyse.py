import pandas as pd, numpy as np
g=pd.read_csv('games.csv'); s=pd.read_csv('seats.csv'); e=pd.read_csv('events.csv'); sh=pd.read_csv('shenanigans.csv')
s['p0']=1/s.seats
def table(col, df=s):
    r=df.groupby(col).agg(n=('winner','size'),wins=('winner','sum'),exp=('p0','sum'),var=('p0',lambda p:(p*(1-p)).sum()))
    r['win%']=100*r.wins/r.n; r['exp%']=100*r.exp/r.n
    r['ratio']=r.wins/r.exp
    r['ci95']=1.96*np.sqrt(r['var'])/r.exp
    r['z']=(r.wins-r.exp)/np.sqrt(r['var'])
    return r[['n','wins','win%','exp%','ratio','ci95','z']].sort_values('ratio',ascending=False).round(2)
pd.set_option('display.width',200)
print("== CIV (all seat counts)"); print(table('civ'))
for n in [3,4,5]:
    t=table('civ',s[s.seats==n]); print(f"-- civ {n}p ratio:", t.ratio.to_dict())
print("== TRAIT"); print(table('trait'))
q=s.groupby(['quest','quest_vp']).agg(n=('quest_done','size'),done=('quest_done','mean'),win=('winner','mean'))
q['done%']=(100*q.done).round(1); q['win_ratio']=(s.groupby(['quest','quest_vp']).apply(lambda d:d.winner.sum()/d.p0.sum())).round(2)
done_w=s[s.quest_done==1].groupby('quest').winner.mean()
print("== QUEST"); print(q[['n','done%','win_ratio']].sort_values('done%',ascending=False))
for n in [3,4,5]:
    print(f"-- quest done% {n}p:", (100*s[s.seats==n].groupby('quest').quest_done.mean()).round(0).astype(int).to_dict())
# pick rate when offered
rows=[]
for _,r in s.iterrows():
    for o in str(r.quest_offer).split('|'):
        rows.append((o, o==r.quest))
po=pd.DataFrame(rows,columns=['quest','picked']).groupby('quest').agg(offered=('picked','size'),pick=('picked','mean'))
print("== quest pick rate when offered"); print(po.round(2))
rows=[]
for _,r in s.iterrows():
    for o in str(r.trait_offer).split('|'):
        rows.append((o, o==r.trait))
print("== trait pick rate"); print(pd.DataFrame(rows,columns=['t','p']).groupby('t').p.mean().round(2))
rows=[]
for _,r in s.iterrows():
    for o in str(r.civ_offer).split('|'):
        rows.append((o, o==r.civ))
print("== civ pick rate"); print(pd.DataFrame(rows,columns=['t','p']).groupby('t').p.mean().round(2))
w=s[s.winner==1]
print("== winner VP breakdown (mean)"); print(w.groupby('seats')[['vp','settlements','cities','longest_road','largest_army','vp_cards','quest_points']].mean().round(2))
print("== all seats VP mean"); print(s.groupby('seats')[['vp','settlements','cities','longest_road','largest_army','vp_cards','quest_points','knights','player_trades']].mean().round(2))
print("share of winners with quest done", w.quest_done.mean().round(3), "non-winners", s[s.winner==0].quest_done.mean().round(3))
print("winners where quest pts were decisive (vp - quest < 10):", ((w.vp-w.quest_points)<10).mean().round(3))
# events
e['delta']=e.hands_after-e.hands_before
base=e.delta.mean()
ev=e.groupby('event').agg(n=('game','size'),per_game=('game',lambda x: len(x)/len(g)),hand_delta=('delta','mean'))
ev['leader_wins%']=(100*e.assign(lw=e.leader_seat==e.winner).groupby('event').lw.mean()).round(1)
print("== EVENTS baseline leader_wins", round(100*(e.leader_seat==e.winner).mean(),1), "hand delta", round(base,2)); print(ev.round(2))
# shenanigans
G=len(g)
k=sh.kind.value_counts(); print("== shenanigan counts per game"); print((k/G).round(2))
b=sh[sh.kind=='ballot']; print("ballots", len(b)); print(b.groupby('item').detail.value_counts(normalize=True).round(2))
print("tiebreaks", (b.seat>=0).mean().round(3))
v=sh[sh.kind=='vote']; print("aye rate by decree"); print(v.groupby('item').detail.apply(lambda d:(d=='aye').mean()).round(2))
a=sh[sh.kind=='auction']; print("auctions",len(a)); print(a.groupby('item').agg(n=('price','size'),nobid=('seat',lambda x:(x<0).mean()),price=('price',lambda p:p[p>0].mean()),bidders=('bidders','mean')).round(2))
a2=a[a.seat>=0]; print("auction winner wins game %", round(100*(a2.seat==a2.winner).mean(),1), "baseline", round(100*(1/a2.seats).mean(),1))
print(a2.assign(w=a2.seat==a2.winner).groupby('item').w.mean().round(3))
bp=sh[sh.kind=='bounty_post']; bc=sh[sh.kind=='bounty_claim']; br=sh[sh.kind=='bounty_return']
print("bounties posted",len(bp),"claimed",len(bc),"returned",len(br),"claim rate",round(len(bc)/max(1,len(bp)),3),"avg reward",bp.price.mean().round(2))
print("bounty target is eventual winner %", round(100*(bp.target==bp.winner).mean(),1), "poster wins %", round(100*(bp.seat==bp.winner).mean(),1),"baseline",round(100*(1/bp.seats).mean(),1))
print("claimer wins %", round(100*(bc.seat==bc.winner).mean(),1))
sp=sh[sh.kind=='sabotage_play']; print("sabotage plays by kind"); print(sp.item.value_counts())
print("sabotage target eventual winner %", round(100*(sp.target==sp.winner).mean(),1), "author wins %", round(100*(sp.seat==sp.winner).mean(),1))
gr=sh[sh.kind=='guess_right']; gw=sh[sh.kind=='guess_wrong']; print("guesses right",len(gr),"wrong",len(gw),"right%",round(len(gr)/max(1,len(gr)+len(gw))*100,1))
bites=sh[sh.kind=='sabotage_bite']; print("bites", bites.item.value_counts().to_dict(), "spoiled lost%", round(100*(bites[bites.item=='SpoiledGrain'].detail=='lost').mean(),1))
print("rerolls", (sh.kind=='reroll').sum())
# usage vs win: did seats that used X win more?
for kind in ['bounty_post','sabotage_play','auction']:
    d=sh[sh.kind==kind][['game','seat','seats','winner']].drop_duplicates(['game','seat'])
    d=d[d.seat>=0]
    print(f"seats that did {kind}: n={len(d)} win%={100*(d.seat==d.winner).mean():.1f} vs baseline {100*(1/d.seats).mean():.1f}")
# game length
print("== rounds"); print(g.groupby('seats').rounds.describe().round(1))
# v142: player trading + knights
print("== TRADES & KNIGHTS per game (by seats)")
tk=g.groupby('seats')[['proposals','accepts','counters','declines','confirms','cancels','knights','knights_preroll']].mean().round(2)
tk['trades/game']=tk.confirms; tk['confirm%']=(100*g.groupby('seats').confirms.sum()/g.groupby('seats').proposals.sum().clip(lower=1)).round(1)
print(tk)
print("largest army held at game end: games %", round(100*(g.largest_army_seat>=0).mean(),1), "seats %", round(100*(s.largest_army>0).mean(),1))
print("largest army holder wins %", round(100*(g[g.largest_army_seat>=0].largest_army_seat==g[g.largest_army_seat>=0].winner).mean(),1))
print("knights per seat", s.knights.mean().round(2), "player trades per seat", s.player_trades.mean().round(2))
print("== seat edge (win% by seat index, fair = 100/seats)")
print((100*s.groupby(['seats','seat']).winner.mean()).round(1).unstack())
