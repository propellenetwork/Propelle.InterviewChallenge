using FastEndpoints;
using Propelle.InterviewChallenge.Application;
using Propelle.InterviewChallenge.Application.Domain;
using Propelle.InterviewChallenge.Application.Domain.Events;

namespace Propelle.InterviewChallenge.Endpoints
{
    public static class MakeDeposit
    {
        public class Request
        {
            public Guid UserId { get; set; }

            public decimal Amount { get; set; }
        }

        public class Response
        {
            public Guid DepositId { get; set; }
        }

        public class Endpoint : Endpoint<Request, Response>
        {
            private readonly PaymentsContext _paymentsContext;
            private readonly Application.EventBus.IEventBus _eventBus;

            public Endpoint(
                PaymentsContext paymentsContext,
                Application.EventBus.IEventBus eventBus)
            {
                _paymentsContext = paymentsContext;
                _eventBus = eventBus;
            }

            public override void Configure()
            {
                Post("/api/deposits/{UserId}");
            }

            public override async Task HandleAsync(Request req, CancellationToken ct)
            {
                var deposit = new Deposit(req.UserId, req.Amount);
                _paymentsContext.Deposits.Add(deposit);

                //If an error is thrown here, then it hasn't saved the data and the tests
                //automatically retry when simulating user behaviour
                await _paymentsContext.SaveChangesAsync(ct);

                bool success = false;
                var retryCount = 7;

                while (!success)
                {
                    try
                    {
                        /*Wrapping this in a while loop with a limit enables us to retry the Publish event
                        if an error is thrown.

                        Ultimately, the retry count could be sent in as an arguement if required.

                        Realistically, a retry would need a "fail safe" if the
                        service was down for a longer period.*/
                        await _eventBus.Publish(new DepositMade { Id = deposit.Id });
                        success = true;
                    }
                    catch when (retryCount-- > 0) { }
                }

                await SendAsync(new Response { DepositId = deposit.Id }, 201, ct);
            }
        }
    }
}
