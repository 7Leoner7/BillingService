using System.Threading.Tasks;
using Grpc.Core;
using BillingService;
using System.Linq;
using System.Text;

namespace BillingService.Services
{
    public class BillingService : Billing.BillingBase
    {
        private readonly ILogger<BillingService> _logger;

        private int lastCoinID = 0;

        private class User
        {
            public string name { get; init; }

            public int rating { get; init; }

            private List<Coin> coins { get; set; }

            public bool GiveUserCoins(List<Coin> _coins)
            {
                try
                {
                    coins.AddRange(_coins);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            readonly static public char ControlSymbol = '-';

            public List<Coin> GetUserCoins() => coins;

            public bool TransactionCoins(User dest_user, int amount)
            {
                try
                {
                    if (amount > coins.Count) throw new Exception();
                    var part_of_coins = coins.Take(amount).ToList();
                    coins.RemoveRange(0, amount);
                    foreach(var coin in part_of_coins) { coin.History += ControlSymbol + dest_user.name; }
                    dest_user.GiveUserCoins(part_of_coins);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public User(string _name, int _rating)
            {
                name = _name;
                rating = _rating;
                coins = new List<Coin>();
            }
        }

        static private User[] users = new User[3] { new User("boris", 5000), new User("maria", 1000), new User("oleg", 800) };

        public BillingService(ILogger<BillingService> logger)
        {
            _logger = logger;
        }

        public override Task ListUsers(None request, IServerStreamWriter<UserProfile> responseStream, ServerCallContext context)
        {
            string Names = "";
            foreach (var user in users)
                Names += user.name + " (Amount money: " + user.GetUserCoins().Count + ", Rating: " + user.rating + ")" + ", ";
            Names = Names.Remove(Names.Length-2, 2);
            Console.WriteLine("Всего " + users.Length + " клиента:\n" + Names);
            
            return responseStream.WriteAsync(new UserProfile
            {
                Amount = users.Length,
                Name = Names
            });
        }
        public override Task<Response> CoinsEmission(EmissionAmount request, ServerCallContext context)
        {
            try
            {
                List<User> u = new List<User>(users);
                u.Sort(new Comparison<User>((el0, el1) => el0.rating > el1.rating ? 1 : el0.rating < el1.rating ? -1 : 0));
                users = u.ToArray();

                var coins = request.Amount;
                var sumRate = 0.0;
                foreach (var user in users) sumRate += user.rating;
                for (int i = 0; i < users.Length; i++) 
                {
                    var quant_coins = Math.Round(coins * users[i].rating / sumRate);
                    quant_coins = quant_coins < 1 ? 1 : quant_coins;
                    coins -= (int)quant_coins;
                    sumRate -= users[i].rating;
                    string zeroHist = "@CoinsEmission"+User.ControlSymbol+users[i].name;
                    List<Coin> coinList = new List<Coin>();
                    for (int n = 0; n < quant_coins; n++)
                    {
                        coinList.Add(new Coin() { History = zeroHist, Id = lastCoinID });
                        lastCoinID++;
                    }
                    users[i].GiveUserCoins(coinList);
                }

                return Task.FromResult(new Response
                {
                   Status = Response.Types.Status.Ok,
                   Comment = "Successfull"
                });
            } catch(Exception)
            {
                return Task.FromResult(new Response
                {
                    Status = Response.Types.Status.Failed,
                    Comment = "Что-то пошло не так :\\\n"
                });
            }
            
        }
        public override Task<Response> MoveCoins(MoveCoinsTransaction request, ServerCallContext context)
        {
            try
            {
                User src_user = users.Where((user) => user.name == request.SrcUser).First();
                User dst_user = users.Where((user) => user.name == request.DstUser).First();
                if (!src_user.TransactionCoins(dst_user, (int)request.Amount)) throw new Exception();
                return Task.FromResult(new Response
                {
                    Status = Response.Types.Status.Ok,
                    Comment = "Successfull"
                });
            }
            catch (Exception)
            {
                return Task.FromResult(new Response
                {
                    Status = Response.Types.Status.Failed,
                    Comment = "Что-то пошло не так :\\\n Пожалуйста, проверьте ваш баланс. Или проверьте имя пользователя."
                });
            }
            
        }
        public override Task<Coin> LongestHistoryCoin(None request, ServerCallContext context)
        {
            var maxL = 0;
            Coin resCoin = new Coin();
            foreach(var user in users)
            {
                var user_coins = user.GetUserCoins();
                foreach(var coin in user_coins)
                {
                    var leng = coin.History.Count((elem) => elem == User.ControlSymbol);
                    if (maxL < leng)
                    {
                        maxL = leng;
                        resCoin = coin;
                    }
                }
            }
            return Task.FromResult(resCoin);
        }
    }
}